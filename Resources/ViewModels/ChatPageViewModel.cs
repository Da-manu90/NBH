using System;
using System.Collections.ObjectModel;
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

        // Lazy Loading Konstanten
        private const int InitialMessageCount = 40;
        private const int PageSize = 20;
        private bool _isLoadingMore = false;
        private int _currentOffset = 0;

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
                        SubscribeToRealtimeMessages(_chatId);
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
        Guid? chatId = null)
        {
            _logger = logger;
            _authService = authService;
            _supabaseClient = supabaseClient;
            _phoenixService = phoenixService ?? throw new ArgumentNullException(nameof(phoenixService));

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
        private async void SubscribeToRealtimeMessages(Guid chatId)
        {
            try
            {
                Console.WriteLine($"[DEBUG] SubscribeToRealtimeMessages => chatId={chatId}");
                await _phoenixService.ConnectAsync();

                // Falls du ein JWT brauchst:
                var userToken = await _authService.GetUserAccessTokenAsync() ?? "";
                var filter = $"chat_id=eq.{chatId}";

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

        // ------------------------------------------------------------
        // Realtime Callback => OnPostgresChanged
        // ------------------------------------------------------------
        private void OnPostgresChanged(object sender, PhoenixPostgresEvent e)
        {
            try
            {
                Console.WriteLine($"[DEBUG] OnPostgresChanged => Table={e.Table}, EventType={e.EventType}");

                if (e.Table != "chat_messages")
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
            await _phoenixService.PushAsync("realtime:public:chat_messages", "typing", payload);
        }

        // --------------------------------------------------
        // Einzelne Event-Handler sauber gekapselt
        // --------------------------------------------------

        private void HandleUpdateEvent(Dictionary<string, object> newRecord)
        {
            var updJson = JsonConvert.SerializeObject(newRecord);
            var updMsg = JsonConvert.DeserializeObject<ChatMessage>(updJson);

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
            var newJson = JsonConvert.SerializeObject(newRecord);
            var newMsg = JsonConvert.DeserializeObject<ChatMessage>(newJson);

            if (newMsg?.ChatId == ChatId)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!Messages.Any(m => m.Id == newMsg.Id))
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
            var oldJson = JsonConvert.SerializeObject(oldRecord);
            var oldMsg = JsonConvert.DeserializeObject<ChatMessage>(oldJson);

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
                // Neue ChatMessage anlegen
                var newMsg = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ChatId = ChatId,
                    SenderId = userGuid,
                    Message = CurrentMessageText,
                    CreatedAt = DateTime.UtcNow
                };

                // 1) Insert in chat_messages
                var insertResp = await _supabaseClient
                    .From<ChatMessage>()
                    .Insert(newMsg);

                if (insertResp.ResponseMessage?.IsSuccessStatusCode == true)
                {
                    Console.WriteLine($"[DEBUG] Inserted ChatMessage => Id={newMsg.Id}, Text='{newMsg.Message}'");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Insert in chat_messages NICHT erfolgreich!");
                }

                // 2) LastMessage & LastMessageDate in public_chat aktualisieren
                //    => .Where(c => c.Id == ChatId).Set(...).Update()
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

                // 3) Eingabefeld leeren
                CurrentMessageText = string.Empty;
                
                // 4) Schreibstatus explizit auf false setzen
                await SendTypingStatusAsync(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SendMessageAsync => {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.MessageSendError);
                });
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

                // Auf Background-Thread ausführen (nicht Main Thread blockieren):
                var messages = await Task.Run(async () =>
                {
                    var response = await _supabaseClient
                        .From<ChatMessage>()
                        .Where(m => m.ChatId == chatId)
                        .Order(m => m.CreatedAt, Ordering.Descending)  // Neueste zuerst
                        .Limit(InitialMessageCount)                     // Nur die letzten 40
                        .Get()
                        .ConfigureAwait(false);

                    // Reihenfolge umkehren für chronologische Anzeige:
                    return response.Models?.OrderBy(m => m.CreatedAt).ToList()
                           ?? new List<ChatMessage>();
                });

                // UI-Update auf Main Thread:
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Messages.Clear();
                    foreach (var msg in messages)
                        Messages.Add(msg);

                    _currentOffset = messages.Count;
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

        // ------------------------------------------------------------
        // LoadMoreMessagesAsync => Ältere Nachrichten nachladen (Lazy Loading)
        // ------------------------------------------------------------
        public async Task LoadMoreMessagesAsync()
        {
            if (_isLoadingMore || ChatId == Guid.Empty) return;
            _isLoadingMore = true;

            try
            {
                var olderMessages = await Task.Run(async () =>
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

                if (olderMessages.Count > 0)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Alte Nachrichten oben einfügen:
                        for (int i = olderMessages.Count - 1; i >= 0; i--)
                            Messages.Insert(0, olderMessages[i]);

                        _currentOffset += olderMessages.Count;
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

        private async Task LoadChatPartnerInfoAsync(Guid partnerId)
        {
            try
            {
                var userResp = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == partnerId)
                    .Get();

                var foundUser = userResp.Models.FirstOrDefault();
                if (foundUser != null)
                {
                    ChatPartnerName = foundUser.Username ?? "Unbekannt";

                    // Aus der DB => ProfileImageUrl => publicURL generieren
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

            // 2) >=12h alt?
            if (chat.CreatedAt.AddHours(12) > DateTime.UtcNow)
            {
                Console.WriteLine("Chat noch keine 12h alt. Popup entfällt.");
                return;
            }

            // 3) Wurde bereits eine ChatReview abgegeben?
            var existingReviewResp = await _supabaseClient
            .From<ChatReview>()
            .Match(new Dictionary<string, string>
            {   
            ["reviewer_id"] = CurrentUserId.ToString(),
            ["reviewed_id"] = chatPartnerId.ToString(),
            ["chat_id"] = chatId.ToString()
            })
            .Get();

            if (existingReviewResp.Models.Any())
            {
                Console.WriteLine("Es existiert bereits eine Chat-Bewertung. Popup entfällt.");
                return;
            }

            // 4) Wurde innerhalb der letzten 72h bereits ein Review für diesen Partner verfasst?
            var partnerReviewResp = await _supabaseClient
                .From<ChatReview>()
                .Where(r => r.reviewer_id == CurrentUserId)
                .Where(r => r.reviewed_id == chatPartnerId)
                .Order("created_at", Ordering.Descending)
                .Get();

            var lastReview = partnerReviewResp.Models.FirstOrDefault();
            if (lastReview != null && lastReview.created_at.AddHours(72) > DateTime.UtcNow)
            {
                Console.WriteLine("Bereits eine Partner-Bewertung in den letzten 72h. Popup entfällt.");
                return;
            }

            // 5) Alles erfüllt => Popup anzeigen
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
