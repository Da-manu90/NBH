using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using NBH.Resources.Models; // Deine Model-Klasse z.B. NotificationModel
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

            await _realtimeService.ConnectAsync();

            var currentUserId = _supabaseClient.Auth.CurrentUser?.Id ?? "";
            Debug.WriteLine($"[NotificationService] Aktuelle UserId: {currentUserId}");

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

            _realtimeService.PostgresChanged += OnPostgresChanged;

            await LoadNotificationsFromDatabaseAsync();

            Debug.WriteLine("[NotificationService] InitializeAsync beendet.");
        }


        public async Task AddNotificationAsync(Guid userId, string message, string type, string actionData)
        {
            var notification = new NotificationModel
            {
                Id = Guid.NewGuid(),
                UserId = userId, // vorher fehlte!
                Type = type,
                Message = message,
                ActionData = actionData,
                CreatedAt = DateTime.UtcNow,
                Read = false
            };

            await _supabaseClient.From<NotificationModel>().Insert(notification);

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
        private async Task LoadNotificationsFromDatabaseAsync()
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

                var response = await _supabaseClient
                    .From<NotificationModel>()
                    .Filter("user_id", Operator.Equals, currentUserId)
                    .Order(order => order.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get()
                    .ConfigureAwait(false);

                Notifications.Clear();
                if (response != null && response.Models.Any())
                {
                    foreach (var item in response.Models)
                    {
                        Notifications.Add(item);
                        Debug.WriteLine($"[NotificationService] Notification geladen: {item.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("[NotificationService] KEINE Notifications in DB gefunden.");
                }

                Debug.WriteLine($"[NotificationService] Gesamt geladen: {Notifications.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Fehler beim Laden: {ex.Message}");
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
                                Notifications.Remove(existing);
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Hilfsfunktion: Dictionary -> NotificationModel
        /// (Achtung: musst du an dein Modell anpassen)
        /// </summary>
        private NotificationModel ConvertDictionaryToNotification(Dictionary<string, object> dict)
        {
            try
            {
                // Minimal: parse ID, user_id, message, ...
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(dict);
                var obj = Newtonsoft.Json.JsonConvert
                    .DeserializeObject<NotificationModel>(json);
                return obj;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Fehler ConvertDictionary: {ex.Message}");
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
    }
}
