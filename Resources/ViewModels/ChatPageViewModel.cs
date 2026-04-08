using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Mopups.Services;
using NBH.Resources.Models;
using NBH.Resources.Views;
using NBH.Services;
using Newtonsoft.Json;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace NBH.Resources.ViewModels
{
    public class ChatPageViewModel : BaseViewModel
    {
        private readonly ILogger<ChatPageViewModel> _logger;
        private readonly AuthService _authService;
        private readonly Supabase.Client _supabaseClient;
        private readonly PhoenixRealtimeService _phoenixService;
        private readonly NotificationService _notificationService;

        // Lazy Loading Konstanten
        private const int InitialMessageCount = 40;
        private const int PageSize = 20;
        private bool _isLoadingMore = false;
        private int _currentOffset = 0;
        private DateTime? _oldestLoadedAt;
        private bool _usingUnifiedConversations;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public Guid CurrentUserId { get; private set; }
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        private string _currentMessageText;
        public string CurrentMessageText
        {
            get => _currentMessageText;
            set
            {
                if (SetProperty(ref _currentMessageText, value))
                {
                    // Bei jeder Textänderung -> Schreibstatus senden (debounced)
                    OnUserTyping();
                }
            }
        }

        private bool _isPartnerTyping;
        public bool IsPartnerTyping
        {
            get => _isPartnerTyping;
            set => SetProperty(ref _isPartnerTyping, value);
        }

        private Timer _typingDebounceTimer;
        private Timer _partnerTypingTimeoutTimer;

        // Commands
        public ICommand SendCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand LoadMoreCommand { get; }

        private Guid _chatPartnerId;
        public Guid ChatPartnerId
        {
            get => _chatPartnerId;
            set => SetProperty(ref _chatPartnerId, value);
        }

        private bool _isGroupConversation;
        public bool IsGroupConversation
        {
            get => _isGroupConversation;
            set => SetProperty(ref _isGroupConversation, value);
        }

        private Guid _chatId;
        public Guid ChatId
        {
            get => _chatId;
            set
            {
                if (SetProperty(ref _chatId, value))
                {
                    _logger.LogInformation($"[ChatPageViewModel] ChatId => {value}");
                    Console.WriteLine($"[DEBUG] ChatId set to => {value}");

                    // Bei neuer ChatId => Laden der Nachrichten + Partner-Infos
                    if (_chatId != Guid.Empty)
                    {
                        LoadMessagesAsync(_chatId);
                        SetChatPartnerInfoAsync(_chatId);
                        _ = SubscribeToRealtimeMessages(_chatId);
                    }
                }
            }
        }

        private string _chatPartnerName;
        public string ChatPartnerName
        {
            get => _chatPartnerName;
            set => SetProperty(ref _chatPartnerName, value);
        }

        private string _chatPartnerImageUrl;
        public string ChatPartnerImageUrl
        {
            get => _chatPartnerImageUrl;
            set => SetProperty(ref _chatPartnerImageUrl, value);
        }

        // ------------------------------------------------------------
        // Konstruktor
        // ------------------------------------------------------------
        public ChatPageViewModel(
        ILogger<ChatPageViewModel> logger,
        AuthService authService,
        Supabase.Client supabaseClient,
        PhoenixRealtimeService phoenixService,
        NotificationService notificationService,
        Guid? chatId = null)
        {
            _logger = logger;
            _authService = authService;
            _supabaseClient = supabaseClient;
            _phoenixService = phoenixService ?? throw new ArgumentNullException(nameof(phoenixService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            // ENTFERNT: Doppelte Registrierung (Zeile 118 behält die Registrierung)

            SendCommand = new Command(async () => await SendMessageAsync());
            GoBackCommand = new Command(async () => await GoBackAsync());
            LoadMoreCommand = new Command(async () => await LoadMoreMessagesAsync());

            // CurrentUserId ermitteln (jetzt synchron – kein Deadlock-Risiko mehr)
            var userStr = _authService.GetCurrentUserId();

            if (!string.IsNullOrEmpty(userStr) && Guid.TryParse(userStr, out var g))
            {
                CurrentUserId = g;
                _logger.LogInformation($"[ChatPageViewModel] CurrentUserId={CurrentUserId}");
                Console.WriteLine($"[DEBUG] CurrentUserId => {CurrentUserId}");
            }
            else
            {
                Console.WriteLine("[DEBUG] Konnte CurrentUserId nicht parsen!");
            }

            Console.WriteLine("[DEBUG] ChatPageViewModel => Subscribing to phoenixService events");
            _phoenixService.PostgresChanged += OnPostgresChanged;
            _phoenixService.BroadcastReceived += OnBroadcastReceived;

            // Falls chatId übergeben wurde => direkt setzen
            if (chatId.HasValue)
            {
                ChatId = chatId.Value; // ruft den Setter auf
            }
            else
            {
                Console.WriteLine("[DEBUG] Kein chatId übergeben => ChatId bleibt Guid.Empty");
            }
        }

        // ------------------------------------------------------------
        // Realtime Subscribing (PhoenixRealtimeService)
        // ------------------------------------------------------------
        private async Task SubscribeToRealtimeMessages(Guid chatId)
        {
            try
            {
                Console.WriteLine($"[DEBUG] SubscribeToRealtimeMessages => chatId={chatId}");
                await _phoenixService.ConnectAsync();

                // Falls du ein JWT brauchst:
                var userToken = await _authService.GetUserAccessTokenAsync() ?? "";
                var filter = $"chat_id=eq.{chatId}";

                _phoenixService.SubscribeChannel(
                    topic: "realtime:public:conversation_messages",
                    userToken: userToken,
                    schema: "public",
                    table: "conversation_messages",
                    filter: $"conversation_id=eq.{chatId}"
                );

                _phoenixService.SubscribeChannel(
                    topic: "realtime:public:chat_messages",
                    userToken: userToken,
                    schema: "public",
                    table: "chat_messages",
                    filter: filter
                );

                _logger.LogInformation($"[ChatPageViewModel] SubscribeToRealtimeMessages => chat_id=eq.{chatId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SubscribeToRealtimeMessages => {ex.Message}");
                _logger.LogError(ex, "[ChatPageViewModel] SubscribeToRealtimeMessages ERROR");
            }
        }

        public void Cleanup()
        {
            _phoenixService.PostgresChanged -= OnPostgresChanged;
            _phoenixService.BroadcastReceived -= OnBroadcastReceived;
            _typingDebounceTimer?.Dispose();
            _partnerTypingTimeoutTimer?.Dispose();
            if (ChatId != Guid.Empty)
            {
                _ = _phoenixService.UnsubscribeChannelAsync("conversation_messages", $"conversation_id=eq.{ChatId}");
                _ = _phoenixService.UnsubscribeChannelAsync("chat_messages", $"chat_id=eq.{ChatId}");
            }
        }

        // ------------------------------------------------------------
        // Realtime Callback => OnPostgresChanged
        // ------------------------------------------------------------
        private void OnPostgresChanged(object sender, PhoenixPostgresEvent e)
        {
            try
            {
                Console.WriteLine($"[DEBUG] OnPostgresChanged => Table={e.Table}, EventType={e.EventType}");

                if (e.Table != "chat_messages" && e.Table != "conversation_messages")
                    return;

                switch (e.EventType.ToUpper())
                {
                    case "INSERT":
                        HandleInsertEvent(e.New);
                        break;

                    case "UPDATE":
                        HandleUpdateEvent(e.New);
                        break;

                    case "DELETE":
                        HandleDeleteEvent(e.Old);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatPageViewModel] OnPostgresChanged - Fehler");
                Console.WriteLine($"[ERROR] OnPostgresChanged => {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // Realtime Broadcast => OnBroadcastReceived (Typing Indicator)
        // ------------------------------------------------------------
        private void OnBroadcastReceived(object sender, PhoenixBroadcastEvent e)
        {
            if (e.Event == "typing")
            {
                try
                {
                    var payload = e.Payload as Dictionary<string, object>;
                    if (payload != null && payload.ContainsKey("is_typing") && payload.ContainsKey("user_id"))
                    {
                        var typingUserId = payload["user_id"].ToString();
                        var isTyping = (bool)payload["is_typing"];

                        // Nur von Partner, nicht von mir selbst
                        if (typingUserId != CurrentUserId.ToString())
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                IsPartnerTyping = isTyping;

                                // Falls Partner aufhört oder Timer abläuft
                                if (isTyping)
                                {
                                    ResetPartnerTypingTimeout();
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[ChatPageViewModel] Fehler beim Verarbeiten von typing broadcast: {ex.Message}");
                }
            }
        }

        private void ResetPartnerTypingTimeout()
        {
            _partnerTypingTimeoutTimer?.Dispose();
            _partnerTypingTimeoutTimer = new Timer((_) =>
            {
                MainThread.BeginInvokeOnMainThread(() => IsPartnerTyping = false);
            }, null, 5000, Timeout.Infinite); // Nach 5 Sek Inaktivität ausblenden
        }

        // ------------------------------------------------------------
        // Eigene Tipp-Erkennung (Senden)
        // ------------------------------------------------------------
        private bool _isLocalTypingSent = false;
        private void OnUserTyping()
        {
            if (!_isLocalTypingSent && !string.IsNullOrWhiteSpace(CurrentMessageText))
            {
                _isLocalTypingSent = true;
                _ = SendTypingStatusAsync(true);
            }

            // Timer zurücksetzen: Wenn 3 Sek nichts getippt wird -> IsTyping=false senden
            _typingDebounceTimer?.Dispose();
            _typingDebounceTimer = new Timer(async (_) =>
            {
                _isLocalTypingSent = false;
                await SendTypingStatusAsync(false);
            }, null, 3000, Timeout.Infinite);
        }

        private async Task SendTypingStatusAsync(bool isTyping)
        {
            if (ChatId == Guid.Empty) return;

            var payload = new { user_id = CurrentUserId, is_typing = isTyping };
            var topic = _usingUnifiedConversations ? "realtime:public:conversation_messages" : "realtime:public:chat_messages";
            await _phoenixService.PushAsync(topic, "typing", payload);
        }

        // --------------------------------------------------
        // Einzelne Event-Handler sauber gekapselt
        // --------------------------------------------------

        private void HandleUpdateEvent(Dictionary<string, object> newRecord)
        {
            var updMsg = DeserializeRealtimeMessage(newRecord);

            if (updMsg?.ChatId == ChatId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existing = Messages.FirstOrDefault(m => m.Id == updMsg.Id);
                    if (existing != null)
                    {
                        var idx = Messages.IndexOf(existing);
                        if (idx >= 0)
                            Messages[idx] = updMsg;

                        OnPropertyChanged(nameof(Messages));
                        Console.WriteLine($"[DEBUG] Nachricht aktualisiert: {updMsg.Message}");
                    }
                });
            }
        }

        private void HandleInsertEvent(Dictionary<string, object> newRecord)
        {
            var newMsg = DeserializeRealtimeMessage(newRecord);

            if (newMsg?.ChatId == ChatId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var isDuplicate =
                        Messages.Any(m => m.Id == newMsg.Id) ||
                        Messages.Any(m =>
                            m.SenderId == newMsg.SenderId &&
                            string.Equals(m.Message?.Trim(), newMsg.Message?.Trim(), StringComparison.Ordinal) &&
                            Math.Abs((m.CreatedAt - newMsg.CreatedAt).TotalSeconds) <= 8);

                    if (!isDuplicate)
                    {
                        Messages.Add(newMsg);
                        Console.WriteLine($"[DEBUG] Nachricht hinzugefügt: {newMsg.Message}");

                        // ➡️ Hier explizit Notify der Collection machen:
                        OnPropertyChanged(nameof(Messages));
                    }
                });
            }
        }


        // Für saubere Struktur (Hilfsmethoden):

        private void HandleDeleteEvent(Dictionary<string, object> oldRecord)
        {
            var oldMsg = DeserializeRealtimeMessage(oldRecord);

            if (oldMsg?.ChatId == ChatId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existing = Messages.FirstOrDefault(m => m.Id == oldMsg.Id);
                    if (existing != null)
                        Messages.Remove(existing);
                });
            }
        }

        private ChatMessage? DeserializeRealtimeMessage(Dictionary<string, object> record)
        {
            if (record == null || record.Count == 0)
            {
                return null;
            }

            if (record.ContainsKey("conversation_id") && !record.ContainsKey("chat_id"))
            {
                record["chat_id"] = record["conversation_id"];
            }

            var json = JsonConvert.SerializeObject(record);
            return JsonConvert.DeserializeObject<ChatMessage>(json);
        }



        // ------------------------------------------------------------
        // SendMessageAsync => Neue Nachricht einfügen
        // ------------------------------------------------------------
        public async Task SendMessageAsync()
        {
            // Falls ChatId oder Eingabetext fehlt -> Abbruch
            if (ChatId == Guid.Empty || string.IsNullOrWhiteSpace(CurrentMessageText))
            {
                Console.WriteLine("[DEBUG] Abbruch: ChatId leer oder Nachricht leer.");
                return;
            }

            // Hole UserId (Guid)
            var userStr = _authService.GetCurrentUserId();
            if (string.IsNullOrEmpty(userStr) || !Guid.TryParse(userStr, out var userGuid))
            {
                Console.WriteLine("[DEBUG] Abbruch: Kein gültiger CurrentUserId!");
                return;
            }

            try
            {
                var messageText = CurrentMessageText.Trim();
                var sentViaUnifiedRpc = await TrySendConversationMessageAsync(messageText).ConfigureAwait(false);
                if (!sentViaUnifiedRpc)
                {
                    // Legacy fallback path
                    var newMsg = new ChatMessage
                    {
                        Id = Guid.NewGuid(),
                        ChatId = ChatId,
                        SenderId = userGuid,
                        Message = messageText,
                        CreatedAt = DateTime.UtcNow
                    };

                    var insertResp = await _supabaseClient
                        .From<ChatMessage>()
                        .Insert(newMsg);

                    if (insertResp.ResponseMessage?.IsSuccessStatusCode != true)
                    {
                        Console.WriteLine("[DEBUG] Insert in chat_messages NICHT erfolgreich!");
                        throw new InvalidOperationException(
                            insertResp.ResponseMessage?.ReasonPhrase ?? "chat_messages insert failed");
                    }

                    Console.WriteLine($"[DEBUG] Inserted ChatMessage => Id={newMsg.Id}, Text='{newMsg.Message}'");

                    var updateResp = await _supabaseClient
                        .From<PublicChat>()
                        .Where(c => c.Id == ChatId)
                        .Set(c => c.LastMessage, newMsg.Message)
                        .Set(c => c.LastMessageDate, newMsg.CreatedAt)
                        .Update();

                    if (updateResp.ResponseMessage?.IsSuccessStatusCode == true)
                    {
                        Console.WriteLine("[DEBUG] public_chat.LastMessage aktualisiert");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Konnte public_chat nicht aktualisieren!");
                    }
                }

                if (sentViaUnifiedRpc)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Messages.Add(new ChatMessage
                        {
                            Id = Guid.NewGuid(),
                            ChatId = ChatId,
                            SenderId = userGuid,
                            Message = messageText,
                            CreatedAt = DateTime.UtcNow
                        });
                    });
                }

                // 3) Eingabefeld leeren
                CurrentMessageText = string.Empty;
                HapticFeedbackHelper.PerformClickFeedback();

                // 4) Schreibstatus explizit auf false setzen
                await SendTypingStatusAsync(false);

                // 5) In-App-Benachrichtigung (RPC create_system_notification) — darf Chat nicht „kaputt“ machen
                if (!IsGroupConversation && ChatPartnerId != Guid.Empty && ChatPartnerId != CurrentUserId)
                {
                    try
                    {
                        var actionData = JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["chat_id"] = ChatId,
                            ["sender_id"] = CurrentUserId
                        });

                        await _notificationService.AddNotificationAsync(
                            ChatPartnerId,
                            "Du hast eine neue Nachricht erhalten.",
                            "chat_message",
                            actionData);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx,
                            "[SendMessageAsync] In-App-Notification fehlgeschlagen (Nachricht wurde gesendet)");
                        Debug.WriteLine($"[SendMessageAsync] Notification-RPC: {notifyEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendMessageAsync => {ex.Message}");
                _logger.LogError(ex, "[SendMessageAsync] Versand fehlgeschlagen");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.MessageSendError);
                });
            }
        }

        private async Task<bool> TrySendConversationMessageAsync(string text)
        {
            try
            {
                var response = await _supabaseClient.Rpc("send_conversation_message_safe", new Dictionary<string, object>
                {
                    { "p_conversation_id", ChatId },
                    { "p_text", text },
                    { "p_metadata", new Dictionary<string, object>() }
                }).ConfigureAwait(false);

                if (response?.ResponseMessage?.IsSuccessStatusCode != true)
                {
                    return false;
                }

                _usingUnifiedConversations = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatPageViewModel] send_conversation_message_safe fehlgeschlagen, fallback aktiv.");
                return false;
            }
        }

        // ------------------------------------------------------------
        // LoadMessagesAsync => Lädt die letzten N Nachrichten (Background Thread)
        // ------------------------------------------------------------
        public async Task LoadMessagesAsync(Guid chatId)
        {
            IsLoading = true;
            try
            {
                Console.WriteLine($"[DEBUG] LoadMessagesAsync => chatId={chatId}");
                List<ChatMessage> messages;
                var unifiedMessages = await TryLoadConversationMessagesAsync(chatId, null, InitialMessageCount).ConfigureAwait(false);
                if (unifiedMessages != null)
                {
                    messages = unifiedMessages.OrderBy(m => m.CreatedAt).ToList();
                }
                else
                {
                    // Legacy fallback
                    messages = await Task.Run(async () =>
                    {
                        var response = await _supabaseClient
                            .From<ChatMessage>()
                            .Where(m => m.ChatId == chatId)
                            .Order(m => m.CreatedAt, Ordering.Descending)
                            .Limit(InitialMessageCount)
                            .Get()
                            .ConfigureAwait(false);

                        return response.Models?.OrderBy(m => m.CreatedAt).ToList()
                               ?? new List<ChatMessage>();
                    });
                }

                // UI-Update auf Main Thread:
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Messages.Clear();
                    foreach (var msg in messages)
                        Messages.Add(msg);

                    _currentOffset = messages.Count;
                    _oldestLoadedAt = messages.FirstOrDefault()?.CreatedAt;
                });

                if (messages.Count > 0)
                    _logger.LogInformation($"[LoadMessagesAsync] {messages.Count} Nachrichten (chatId={chatId}) geladen.");
                else
                    Console.WriteLine("[DEBUG] Keine Nachrichten gefunden oder Fehler bei der Abfrage.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoadMessagesAsync] Fehler");
                Console.WriteLine("[ERROR] LoadMessagesAsync => " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<List<ChatMessage>?> TryLoadConversationMessagesAsync(Guid conversationId, DateTime? before, int limit)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_conversation_id", conversationId },
                    { "p_before", before },
                    { "p_limit", limit }
                };

                var response = await _supabaseClient.Rpc("get_conversation_messages_safe", rpcParams).ConfigureAwait(false);
                if (response?.ResponseMessage?.IsSuccessStatusCode != true)
                {
                    return null;
                }

                var messageItems = string.IsNullOrWhiteSpace(response.Content)
                    ? new List<ConversationMessageRpcItem>()
                    : JsonConvert.DeserializeObject<List<ConversationMessageRpcItem>>(response.Content) ?? new List<ConversationMessageRpcItem>();

                _usingUnifiedConversations = true;
                return messageItems.Select(x => new ChatMessage
                {
                    Id = x.Id,
                    ChatId = x.ConversationId,
                    SenderId = x.SenderId,
                    Message = x.Message,
                    CreatedAt = x.CreatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatPageViewModel] get_conversation_messages_safe fehlgeschlagen, fallback aktiv.");
                return null;
            }
        }

        // ------------------------------------------------------------
        // LoadMoreMessagesAsync => Ältere Nachrichten nachladen (Lazy Loading)
        // ------------------------------------------------------------
        public async Task LoadMoreMessagesAsync()
        {
            if (_isLoadingMore || ChatId == Guid.Empty) return;
            _isLoadingMore = true;

            try
            {
                List<ChatMessage> olderMessages;
                if (_usingUnifiedConversations)
                {
                    var older = await TryLoadConversationMessagesAsync(ChatId, _oldestLoadedAt, PageSize).ConfigureAwait(false);
                    olderMessages = older?.OrderBy(m => m.CreatedAt).ToList() ?? new List<ChatMessage>();
                }
                else
                {
                    olderMessages = await Task.Run(async () =>
                    {
                        var response = await _supabaseClient
                            .From<ChatMessage>()
                            .Where(m => m.ChatId == ChatId)
                            .Order(m => m.CreatedAt, Ordering.Descending)
                            .Range(_currentOffset, _currentOffset + PageSize - 1)
                            .Get()
                            .ConfigureAwait(false);

                        return response.Models?.OrderBy(m => m.CreatedAt).ToList()
                               ?? new List<ChatMessage>();
                    });
                }

                if (olderMessages.Count > 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Alte Nachrichten oben einfügen:
                        for (int i = olderMessages.Count - 1; i >= 0; i--)
                            Messages.Insert(0, olderMessages[i]);

                        _currentOffset += olderMessages.Count;
                        _oldestLoadedAt = Messages.FirstOrDefault()?.CreatedAt;
                    });

                    Console.WriteLine($"[LoadMoreMessagesAsync] {olderMessages.Count} ältere Nachrichten nachgeladen.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoadMoreMessagesAsync] Fehler");
                Console.WriteLine("[ERROR] LoadMoreMessagesAsync => " + ex.Message);
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        // ------------------------------------------------------------
        // SetChatPartnerInfoAsync => Ermittelt PartnerId & lädt Name/Bild
        // ------------------------------------------------------------
        private async Task SetChatPartnerInfoAsync(Guid chatId)
        {
            try
            {
                if (await TrySetConversationHeaderAsync(chatId).ConfigureAwait(false))
                {
                    return;
                }

                var chatResp = await _supabaseClient
                    .From<PublicChat>()
                    .Where(c => c.Id == chatId)
                    .Get();

                var chat = chatResp.Models.FirstOrDefault();
                if (chat == null)
                {
                    Console.WriteLine("[DEBUG] Kein Chat gefunden mit Id => " + chatId);
                    return;
                }

                // Wer ist der Partner? (der jeweils andere)
                if (Guid.TryParse(chat.UserId, out var userGuid) &&
                    Guid.TryParse(chat.PartnerId, out var partnerGuid))
                {
                    var actualPartnerId = (CurrentUserId == userGuid) ? partnerGuid : userGuid;
                    ChatPartnerId = actualPartnerId; // UI-Bound Property

                    // Partner-Infos laden
                    await LoadChatPartnerInfoAsync(actualPartnerId);
                }
                else
                {
                    Console.WriteLine("[DEBUG] user_id oder partner_id konnte nicht in GUID konvertiert werden.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SetChatPartnerInfoAsync] Fehler");
                Console.WriteLine("[ERROR] SetChatPartnerInfoAsync => " + ex.Message);
            }
        }

        private async Task<bool> TrySetConversationHeaderAsync(Guid conversationId)
        {
            try
            {
                var response = await _supabaseClient.Rpc("get_user_conversations_safe", new Dictionary<string, object>
                {
                    { "p_user_id", CurrentUserId },
                    { "p_limit", 200 },
                    { "p_offset", 0 },
                    { "p_filter", "all" }
                }).ConfigureAwait(false);

                if (response?.ResponseMessage?.IsSuccessStatusCode != true || string.IsNullOrWhiteSpace(response.Content))
                {
                    return false;
                }

                var conversations = JsonConvert.DeserializeObject<List<ConversationListRpcItem>>(response.Content)
                                   ?? new List<ConversationListRpcItem>();
                var conversation = conversations.FirstOrDefault(x => x.ConversationId == conversationId);
                if (conversation == null)
                {
                    return false;
                }

                IsGroupConversation = string.Equals(conversation.ConversationType, "group", StringComparison.OrdinalIgnoreCase);
                ChatPartnerId = conversation.PartnerId ?? Guid.Empty;
                ChatPartnerName = IsGroupConversation
                    ? (conversation.Title ?? "Gruppenchat")
                    : (conversation.PartnerName ?? "Unbekannt");

                var avatar = conversation.AvatarUrl;
                if (!string.IsNullOrWhiteSpace(avatar) && !avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !IsGroupConversation)
                {
                    avatar = _supabaseClient.Storage.From("profile_pictures").GetPublicUrl(avatar);
                }

                ChatPartnerImageUrl = string.IsNullOrWhiteSpace(avatar)
                    ? "profile_placeholder.png"
                    : avatar;

                _usingUnifiedConversations = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatPageViewModel] get_user_conversations_safe Header-Fallback fehlgeschlagen.");
                return false;
            }
        }

        private async Task LoadChatPartnerInfoAsync(Guid partnerId)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_user_id", partnerId }
                };

                var response = await _supabaseClient.Rpc("get_public_user_profile", rpcParams);
                var foundUser = string.IsNullOrWhiteSpace(response?.Content)
                    ? null
                    : JsonConvert.DeserializeObject<PublicUserProfile>(response.Content);

                if (foundUser != null)
                {
                    ChatPartnerName = foundUser.Username ?? "Unbekannt";

                    ChatPartnerImageUrl = string.IsNullOrEmpty(foundUser.ProfileImageUrl)
                        ? "profile_placeholder.png"
                        : _supabaseClient.Storage
                                         .From("profile_pictures")
                                         .GetPublicUrl(foundUser.ProfileImageUrl);

                    Console.WriteLine($"[DEBUG] ChatPartnerName={ChatPartnerName}, ChatPartnerImageUrl={ChatPartnerImageUrl}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Kein passender User für PartnerId={partnerId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LoadChatPartnerInfoAsync] Fehler");
                Console.WriteLine($"[ERROR] LoadChatPartnerInfoAsync => {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // MarkChatAsReadAsync => Setzt user_last_read_at oder partner_last_read_at
        // ------------------------------------------------------------
        public async Task MarkChatAsReadAsync()
        {
            if (ChatId == Guid.Empty || CurrentUserId == Guid.Empty)
            {
                Console.WriteLine("[MarkChatAsReadAsync] ChatId oder CurrentUserId fehlt. Abbruch.");
                return;
            }

            try
            {
                var unifiedResponse = await _supabaseClient.Rpc("mark_conversation_read_self_safe", new Dictionary<string, object>
                {
                    { "p_conversation_id", ChatId }
                });
                if (unifiedResponse?.ResponseMessage?.IsSuccessStatusCode == true)
                {
                    _usingUnifiedConversations = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MarkChatAsReadAsync] mark_conversation_read_self_safe fehlgeschlagen, fallback auf legacy.");
            }

            try
            {
                var chatResp = await _supabaseClient
                    .From<PublicChat>()
                    .Where(c => c.Id == ChatId)
                    .Get();

                var chat = chatResp.Models.FirstOrDefault();
                if (chat == null)
                {
                    Console.WriteLine("[MarkChatAsReadAsync] Kein Chat gefunden => Abbruch.");
                    return;
                }

                var nowUtc = DateTime.UtcNow;

                // Prüfen, ob CurrentUser der user_id oder partner_id entspricht
                if (chat.UserId == CurrentUserId.ToString())
                {
                    // user_last_read_at updaten
                    await _supabaseClient
                        .From<PublicChat>()
                        .Where(c => c.Id == ChatId)
                        .Set(c => c.UserLastReadAt, nowUtc)
                        .Update();
                }
                else if (chat.PartnerId == CurrentUserId.ToString())
                {
                    // partner_last_read_at updaten
                    await _supabaseClient
                        .From<PublicChat>()
                        .Where(c => c.Id == ChatId)
                        .Set(c => c.PartnerLastReadAt, nowUtc)
                        .Update();
                }

                Console.WriteLine("[MarkChatAsReadAsync] Chat als gelesen markiert.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] MarkChatAsReadAsync => {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // CheckForReviewPopup => Zeigt evtl. ein Review-Mopup an
        // ------------------------------------------------------------
        public async Task CheckForReviewPopup(Guid chatId, Guid chatPartnerId)
        {
            // 1) Chat holen
            var chatResponse = await _supabaseClient
                .From<PublicChat>()
                .Where(c => c.Id == chatId)
                .Get();

            var chat = chatResponse.Models.FirstOrDefault();
            if (chat == null)
            {
                Console.WriteLine("Kein Chat mit der angegebenen ID gefunden. Popup entfällt.");
                return;
            }

            var rpcParams = new Dictionary<string, object>
            {
                { "p_chat_id", chatId },
                { "p_reviewed_id", chatPartnerId }
            };

            var eligibilityResponse = await _supabaseClient.Rpc("can_show_chat_review_popup_safe", rpcParams);
            var canShowPopup = !string.IsNullOrWhiteSpace(eligibilityResponse?.Content) &&
                               JsonConvert.DeserializeObject<bool>(eligibilityResponse.Content);

            if (!canShowPopup)
            {
                Console.WriteLine("Bewertungspopup laut Eligibility-RPC nicht erlaubt. Popup entfällt.");
                return;
            }

            // 3) Alles erfüllt => Popup anzeigen
            Console.WriteLine("Bewertungspopup wird geöffnet...");
            try
            {
                var reviewerId = CurrentUserId;
                var reviewedId = chatPartnerId;

                await MopupService.Instance.PushAsync(new ChatReviewMopup(chatId, reviewerId, reviewedId, _authService));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Fehler beim Öffnen des Popups: {ex.Message}");
            }
        }

        // ------------------------------------------------------------
        // GoBackAsync => Zurück zur ChatViewPage
        // ------------------------------------------------------------
        private async Task GoBackAsync()
        {
            try
            {
                Console.WriteLine("[DEBUG] GoBackAsync => Navigating to //ChatViewPage");
                await Shell.Current.GoToAsync("//ChatViewPage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GoBackAsync] Fehler");
                Console.WriteLine($"[ERROR] GoBackAsync => {ex.Message}");
            }
        }
    }
}
