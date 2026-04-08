using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBH.Resources.Models; // Deine Model-Klasse z.B. NotificationModel
using Newtonsoft.Json;
using static Supabase.Postgrest.Constants;
using SupabaseClient = Supabase.Client;
using OneSignalSDK.DotNet;
using GotrueClient = Supabase.Gotrue.Client;


namespace NBH.Services
{
    /// <summary>
    /// Service, der sich um das Laden & Realtime-Empfangen
    /// von Notifications kümmert.
    /// </summary>
    public class NotificationService
    {
        private readonly SupabaseClient _supabaseClient;
        private readonly PhoenixRealtimeService _realtimeService;
        private string? _initializedUserId;

        /// <summary>
        /// Alle aktuell geladenen Notifications im Speicher.
        /// Einfaches Beispiel; du kannst auch ein eigenes ViewModel machen.
        /// </summary>
        public ObservableCollection<NotificationModel> Notifications { get; private set; }
            = new ObservableCollection<NotificationModel>();

        public NotificationService(SupabaseClient supabaseClient, PhoenixRealtimeService realtimeService)
        {
            _supabaseClient = supabaseClient
                ?? throw new ArgumentNullException(nameof(supabaseClient));

            _realtimeService = realtimeService
                ?? throw new ArgumentNullException(nameof(realtimeService));
        }

        /// <summary>
        /// Wird beim App-Start oder beim erstmaligen Aufruf
        /// aufgerufen, um Realtime & Auth zu sichern.
        /// </summary>
        public async Task InitializeAsync()
        {
            Debug.WriteLine("[NotificationService] InitializeAsync gestartet.");

            await _supabaseClient.InitializeAsync();

            var currentUserId = _supabaseClient.Auth.CurrentUser?.Id ?? "";
            Debug.WriteLine($"[NotificationService] Aktuelle UserId: {currentUserId}");

            if (!string.IsNullOrEmpty(currentUserId) && _initializedUserId == currentUserId)
            {
                Debug.WriteLine("[NotificationService] Bereits fuer aktuellen User initialisiert.");
                return;
            }

            await _realtimeService.ConnectAsync();

            if (!string.IsNullOrEmpty(currentUserId))
            {
                // **NEU: OneSignal-User-Login**
                // Verknüpft die externe Supabase UserId mit der OneSignal PlayerId
                OneSignal.Login(currentUserId);
                Debug.WriteLine($"[NotificationService] OneSignal Login für {currentUserId} erfolgt.");

                _realtimeService.SubscribeChannel(
                    topic: "realtime:public:notifications",
                    userToken: "",
                    schema: "public",
                    table: "notifications",
                    filter: $"user_id=eq.{currentUserId}"
                );
                Debug.WriteLine("[NotificationService] Realtime Channel subscribed.");
            }
            else
            {
                Debug.WriteLine("[NotificationService] UserId leer => kein Channel subscribed.");
            }

            _realtimeService.PostgresChanged -= OnPostgresChanged;
            _realtimeService.PostgresChanged += OnPostgresChanged;

            await LoadNotificationsFromDatabaseAsync(limit: 15, offset: 0, unreadOnly: false);
            _initializedUserId = currentUserId;

            Debug.WriteLine("[NotificationService] InitializeAsync beendet.");
        }


        public async Task AddNotificationAsync(Guid userId, string message, string type, string actionData)
        {
            object? actionPayload = null;
            if (!string.IsNullOrWhiteSpace(actionData))
            {
                try
                {
                    actionPayload = JsonConvert.DeserializeObject<object>(actionData);
                }
                catch
                {
                    actionPayload = new Dictionary<string, object>
                    {
                        ["raw"] = actionData
                    };
                }
            }

            var rpcParams = new Dictionary<string, object>
            {
                { "p_user_id", userId },
                { "p_type", type },
                { "p_message", message },
                { "p_action_data", actionPayload ?? new Dictionary<string, object>() }
            };

            try
            {
                await _supabaseClient.Rpc("create_system_notification", rpcParams).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Notifications sind "best effort" und duerfen nie den eigentlichen
                // Fach-Workflow (z.B. Chat-Nachricht senden) abbrechen.
                Debug.WriteLine($"[NotificationService] create_system_notification fehlgeschlagen: {ex.Message}");
            }
        }

        public event EventHandler<NotificationModel> NewNotificationReceived;

        private void NotifyNewNotification(NotificationModel newNotification)
        {
            NewNotificationReceived?.Invoke(this, newNotification);
        }


        /// <summary>
        /// Beispiel: Lade alle Notifications des aktuellen Users
        /// aus Supabase, sortiere nach created_at DESC.
        /// </summary>
        private async Task LoadNotificationsFromDatabaseAsync(int limit, int offset, bool unreadOnly)
        {
            try
            {
                var currentUserId = _supabaseClient.Auth.CurrentUser?.Id;
                Debug.WriteLine($"[NotificationService] Lade Notifications für User: {currentUserId}");

                if (string.IsNullOrEmpty(currentUserId))
                {
                    Debug.WriteLine("[NotificationService] Keine User-ID verfügbar => Abbruch");
                    return;
                }

                var models = await TryLoadNotificationsViaRpcAsync(limit, offset, unreadOnly).ConfigureAwait(false);
                if (models.Count == 0)
                {
                    // Fallback for environments where RPC is not deployed yet.
                    var response = await _supabaseClient
                        .From<NotificationModel>()
                        .Filter("user_id", Operator.Equals, currentUserId)
                        .Order(order => order.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                        .Limit(limit)
                        .Get()
                        .ConfigureAwait(false);

                    models = response?.Models?.ToList() ?? new List<NotificationModel>();
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Notifications.Clear();

                    foreach (var item in models)
                    {
                        Notifications.Add(item);
                        Debug.WriteLine($"[NotificationService] Notification geladen: {item.Message}");
                    }

                    if (models.Count == 0)
                    {
                        Debug.WriteLine("[NotificationService] KEINE Notifications in DB gefunden.");
                    }

                    Debug.WriteLine($"[NotificationService] Gesamt geladen: {Notifications.Count}");
                    OnNotificationsUpdated();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Fehler beim Laden: {ex.Message}");
            }
        }

        private async Task<List<NotificationModel>> TryLoadNotificationsViaRpcAsync(int limit, int offset, bool unreadOnly)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_limit", limit },
                    { "p_offset", offset },
                    { "p_unread_only", unreadOnly }
                };

                var rpcResponse = await _supabaseClient
                    .Rpc("get_notifications_for_user", rpcParams)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(rpcResponse?.Content))
                {
                    return new List<NotificationModel>();
                }

                return JsonConvert.DeserializeObject<List<NotificationModel>>(rpcResponse.Content)
                    ?? new List<NotificationModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] RPC get_notifications_for_user fehlgeschlagen: {ex.Message}");
                return new List<NotificationModel>();
            }
        }



        /// <summary>
        /// Wird ausgelöst, wenn Realtime "postgres_changes" meldet.
        /// </summary>
        private void OnPostgresChanged(object sender, PhoenixPostgresEvent e)
        {
            Debug.WriteLine("[NotificationService] Realtime-Event empfangen: " + e.EventType);

            if (e.Table == "notifications")
            {
                if (e.EventType == "INSERT")
                {
                    var newObj = ConvertDictionaryToNotification(e.New);
                    if (newObj != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // Prüfen, ob bereits vorhanden
                            if (!Notifications.Any(x => x.Id == newObj.Id))
                            {
                                Notifications.Insert(0, newObj);
                                NotifyNewNotification(newObj); // Ereignis nur hier feuern!
                                OnNotificationsUpdated();
                            }
                        });
                    }
                }
                else if (e.EventType == "UPDATE")
                {
                    var updated = ConvertDictionaryToNotification(e.New);
                    if (updated != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var old = Notifications.FirstOrDefault(x => x.Id == updated.Id);
                            if (old != null)
                            {
                                var idx = Notifications.IndexOf(old);
                                Notifications[idx] = updated;
                                OnNotificationsUpdated();
                            }
                        });
                    }
                }
                else if (e.EventType == "DELETE")
                {
                    var oldObj = ConvertDictionaryToNotification(e.Old);
                    if (oldObj != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var existing = Notifications.FirstOrDefault(x => x.Id == oldObj.Id);
                            if (existing != null)
                            {
                                Notifications.Remove(existing);
                                OnNotificationsUpdated();
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Hilfsfunktion: Dictionary -> NotificationModel
        /// (Achtung: musst du an dein Modell anpassen)
        /// </summary>
        private NotificationModel? ConvertDictionaryToNotification(Dictionary<string, object> dict)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
                var model = Newtonsoft.Json.JsonConvert.DeserializeObject<NotificationModel>(json);
                return model;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Deserialisierung fehlgeschlagen: {ex.Message}");
                return null;
            }
        }

        public event EventHandler NotificationsUpdated;

        protected void OnNotificationsUpdated()
        {
            NotificationsUpdated?.Invoke(this, EventArgs.Empty);
        }

        // Diese Methode kannst du dann aus dem ViewModel aufrufen
        public void RaiseNotificationsUpdated()
        {
            OnNotificationsUpdated();
        }

        public Task ReloadAsync()
        {
            return LoadNotificationsFromDatabaseAsync(limit: 15, offset: 0, unreadOnly: false);
        }

        public Task ReloadForPopupAsync(bool unreadOnly = false)
        {
            // Popup should stay snappy: smaller page, optional unread-only mode.
            return LoadNotificationsFromDatabaseAsync(limit: 12, offset: 0, unreadOnly: unreadOnly);
        }
    }
}
