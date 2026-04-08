using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Services;
using Newtonsoft.Json;

namespace NBH.Resources.ViewModels
{
    public class ChatViewPageViewModel : BaseViewModel
    {
        private readonly ILogger<ChatViewPageViewModel> _logger;
        private readonly AuthService _authService;
        private readonly Supabase.Client _supabaseClient;
        private readonly PhoenixRealtimeService _phoenixRealtimeService;
        private int _reloadScheduled;
        private bool _isRealtimeSubscribed;
        private bool _isActive;

        public ObservableCollection<PublicChat> Chats { get; set; } = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotLoading));
            }
        }

        public bool IsNotLoading => !IsLoading;

        public ICommand RefreshCommand { get; }
        public ICommand ArchiveCommand { get; }

        public ChatViewPageViewModel(
            ILogger<ChatViewPageViewModel> logger,
            AuthService authService,
            Supabase.Client supabaseClient,
            PhoenixRealtimeService phoenixRealtimeService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _phoenixRealtimeService = phoenixRealtimeService ?? throw new ArgumentNullException(nameof(phoenixRealtimeService));

            RefreshCommand = new Command(async () => await LoadChatsAsync());
            ArchiveCommand = new Command<PublicChat>(async chat => await ArchiveChatAsync(chat));
        }

        public Task StartRealtimeAsync()
        {
            _isActive = true;
            if (_isRealtimeSubscribed)
            {
                return Task.CompletedTask;
            }

            _phoenixRealtimeService.PostgresChanged -= OnRealtimePostgresChanged;
            _phoenixRealtimeService.PostgresChanged += OnRealtimePostgresChanged;
            _phoenixRealtimeService.SubscribeChannel(
                topic: "realtime:public:conversation_messages",
                userToken: string.Empty,
                schema: "public",
                table: "conversation_messages");
            _phoenixRealtimeService.SubscribeChannel(
                topic: "realtime:public:conversations",
                userToken: string.Empty,
                schema: "public",
                table: "conversations");

            _isRealtimeSubscribed = true;
            return Task.CompletedTask;
        }

        public void Cleanup()
        {
            _isActive = false;
            if (_isRealtimeSubscribed)
            {
                _phoenixRealtimeService.PostgresChanged -= OnRealtimePostgresChanged;
                _isRealtimeSubscribed = false;
            }
        }

        public async Task LoadChatsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("[ChatViewPageViewModel] Lade Konversationen via RPC...");

                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
                    return;

                var conversations = await LoadConversationsSafeAsync(userGuid).ConfigureAwait(false);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Chats.Clear();
                    foreach (var conversation in conversations.OrderByDescending(c => c.LastMessageDate))
                    {
                        Chats.Add(conversation);
                    }
                });

                _logger.LogInformation("[ChatViewPageViewModel] {Count} Konversationen geladen.", conversations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatViewPageViewModel] Fehler beim Laden der Konversationen.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<List<PublicChat>> LoadConversationsSafeAsync(Guid userGuid)
        {
            try
            {
                var response = await _supabaseClient.Rpc("get_user_conversations_safe", new Dictionary<string, object>
                {
                    { "p_user_id", userGuid },
                    { "p_limit", 100 },
                    { "p_offset", 0 },
                    { "p_filter", "all" }
                }).ConfigureAwait(false);

                var conversationItems = string.IsNullOrWhiteSpace(response?.Content)
                    ? new List<ConversationListRpcItem>()
                    : JsonConvert.DeserializeObject<List<ConversationListRpcItem>>(response.Content) ?? new List<ConversationListRpcItem>();

                return conversationItems
                    .OrderByDescending(c => c.LastMessageDate)
                    .Select(MapRpcConversationToPublicChat)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatViewPageViewModel] Unified RPC nicht verfügbar, fallback auf get_user_chats_safe.");
            }

            var fallbackResponse = await _supabaseClient.Rpc("get_user_chats_safe", new Dictionary<string, object>
            {
                { "p_user_id", userGuid }
            }).ConfigureAwait(false);

            var fallbackItems = string.IsNullOrWhiteSpace(fallbackResponse?.Content)
                ? new List<ChatListRpcItem>()
                : JsonConvert.DeserializeObject<List<ChatListRpcItem>>(fallbackResponse.Content) ?? new List<ChatListRpcItem>();

            return fallbackItems
                .OrderByDescending(c => c.LastMessageDate)
                .Select(MapRpcChatToPublicChat)
                .ToList();
        }

        private async Task ArchiveChatAsync(PublicChat chat)
        {
            if (chat == null)
                return;

            try
            {
                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return;

                if (await TryArchiveConversationAsync(chat.Id).ConfigureAwait(false))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (Chats.Contains(chat))
                        {
                            Chats.Remove(chat);
                        }
                    });

                    _logger.LogInformation("Konversation {ChatId} archiviert (unified RPC).", chat.Id);
                    return;
                }

                var existingChatResp = await _supabaseClient
                    .From<PublicChat>()
                    .Where(c => c.Id == chat.Id)
                    .Get()
                    .ConfigureAwait(false);

                var existingChat = existingChatResp.Models.FirstOrDefault();
                if (existingChat == null)
                {
                    _logger.LogWarning("Chat {ChatId} konnte vor dem Archivieren nicht geladen werden.", chat.Id);
                    return;
                }

                Supabase.Postgrest.Responses.ModeledResponse<PublicChat>? updateResp;
                if (existingChat.UserId == userId)
                {
                    updateResp = await _supabaseClient
                        .From<PublicChat>()
                        .Where(c => c.Id == chat.Id)
                        .Set(c => c.ArchivedByUser, true)
                        .Update()
                        .ConfigureAwait(false);
                }
                else if (existingChat.PartnerId == userId)
                {
                    updateResp = await _supabaseClient
                        .From<PublicChat>()
                        .Where(c => c.Id == chat.Id)
                        .Set(c => c.ArchivedByPartner, true)
                        .Update()
                        .ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning("User {UserId} ist an Chat {ChatId} nicht beteiligt.", userId, chat.Id);
                    return;
                }

                if (updateResp?.Models.Any() == true)
                {
                    if (Chats.Contains(chat))
                    {
                        Chats.Remove(chat);
                    }

                    _logger.LogInformation("Chat {ChatId} archiviert für User {UserId}.", chat.Id, userId);
                }
                else
                {
                    _logger.LogWarning("Keine Rückgabe beim Updaten: Chat {ChatId} wurde evtl. nicht archiviert.", chat.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Archivieren von Chat {ChatId}.", chat.Id);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ChatArchiveError);
                });
            }
        }

        private async Task<bool> TryArchiveConversationAsync(Guid conversationId)
        {
            try
            {
                var response = await _supabaseClient.Rpc("archive_conversation_self_safe", new Dictionary<string, object>
                {
                    { "p_conversation_id", conversationId }
                }).ConfigureAwait(false);

                if (response?.ResponseMessage?.IsSuccessStatusCode != true)
                {
                    return false;
                }

                var content = response.Content?.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    return true;
                }

                return content.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatViewPageViewModel] archive_conversation_self_safe fehlgeschlagen.");
                return false;
            }
        }

        private PublicChat MapRpcChatToPublicChat(ChatListRpcItem chat)
        {
            var partnerImage = chat.PartnerImage;
            if (!string.IsNullOrEmpty(partnerImage) && !partnerImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                partnerImage = _supabaseClient.Storage.From("profile_pictures").GetPublicUrl(partnerImage);
            }
            else if (string.IsNullOrEmpty(partnerImage))
            {
                partnerImage = "profile_placeholder.png";
            }

            return new PublicChat
            {
                Id = chat.ChatId,
                PartnerId = chat.PartnerId.ToString(),
                PartnerName = chat.PartnerName ?? "Unbekannt",
                PartnerImageUrl = partnerImage,
                LastMessage = chat.LastMessage ?? string.Empty,
                LastMessageDate = chat.LastMessageDate,
                IsUnread = chat.IsUnread,
                ArchivedByUser = chat.ArchivedByUser,
                ArchivedByPartner = chat.ArchivedByPartner,
                ConversationType = "dm",
                MemberCount = 2
            };
        }

        private PublicChat MapRpcConversationToPublicChat(ConversationListRpcItem conversation)
        {
            var avatar = conversation.AvatarUrl;
            if (!string.IsNullOrWhiteSpace(avatar) &&
                !avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                conversation.ConversationType?.Equals("group", StringComparison.OrdinalIgnoreCase) != true)
            {
                avatar = _supabaseClient.Storage.From("profile_pictures").GetPublicUrl(avatar);
            }

            if (string.IsNullOrWhiteSpace(avatar))
            {
                avatar = "profile_placeholder.png";
            }

            var isGroup = conversation.ConversationType?.Equals("group", StringComparison.OrdinalIgnoreCase) == true;
            var displayName = isGroup
                ? (conversation.Title ?? "Gruppe")
                : (conversation.PartnerName ?? "Unbekannt");

            return new PublicChat
            {
                Id = conversation.ConversationId,
                PartnerId = (conversation.PartnerId ?? Guid.Empty).ToString(),
                PartnerName = displayName,
                PartnerImageUrl = avatar,
                LastMessage = conversation.LastMessage ?? string.Empty,
                LastMessageDate = conversation.LastMessageDate,
                IsUnread = conversation.IsUnread,
                ConversationType = isGroup ? "group" : "dm",
                MemberCount = conversation.MemberCount <= 0 ? (isGroup ? 1 : 2) : conversation.MemberCount
            };
        }

        private void OnRealtimePostgresChanged(object? sender, PhoenixPostgresEvent e)
        {
            if (!_isActive)
            {
                return;
            }

            if (e.Table != "conversation_messages" && e.Table != "conversations")
            {
                return;
            }

            ScheduleChatListReload();
        }

        private void ScheduleChatListReload()
        {
            if (Interlocked.Exchange(ref _reloadScheduled, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    if (_isActive)
                    {
                        await LoadChatsAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[ChatViewPageViewModel] Realtime-Reload fehlgeschlagen.");
                }
                finally
                {
                    Interlocked.Exchange(ref _reloadScheduled, 0);
                }
            });
        }
    }
}
