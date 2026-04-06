using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Resources.Controls;
using NBH.Services;
using Newtonsoft.Json.Linq;
using Mopups.Services;
using Supabase;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using static Supabase.Postgrest.Constants;

namespace NBH.Resources.ViewModels
{
    public partial class NotificationListViewModel
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<NotificationListViewModel> _logger;
        private readonly AuthService _authService;
        private readonly NotificationService _notificationService;

        public ObservableCollection<NotificationModel> Notifications
            => _notificationService.Notifications;

        public ICommand RefreshCommand { get; }
        public ICommand RatingCommand { get; }

        public NotificationListViewModel(
            NotificationService notificationService, // <-- NEU
            Supabase.Client supabaseClient,
            ILogger<NotificationListViewModel> logger,
            AuthService authService)
        {
            _notificationService = notificationService; // <-- merken
            _supabaseClient = supabaseClient;
            _logger = logger;
            _authService = authService;

            RefreshCommand = new AsyncRelayCommand(LoadNotificationsFromDatabaseAsync);
            RatingCommand = new AsyncRelayCommand<NotificationModel>(OnRatingAsync);
        }

        private async Task LoadNotificationsFromDatabaseAsync()
        {
            try
            {
                var currentUserId = _supabaseClient.Auth.CurrentUser?.Id;
                if (string.IsNullOrEmpty(currentUserId))
                    return;

                var response = await _supabaseClient
                    .From<NotificationModel>()
                    .Filter("user_id", Operator.Equals, currentUserId)
                    .Order(order => order.CreatedAt, Ordering.Descending)
                    .Get();

                Notifications.Clear();
                if (response?.Models != null)
                {
                    foreach (var item in response.Models)
                        Notifications.Add(item);
                }

                Debug.WriteLine($"[NotificationService] Notifications geladen: {Notifications.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Fehler beim Laden: {ex.Message}");
            }
        }

        // "Bewertung abgeben"-Button
        private async Task OnRatingAsync(NotificationModel notif)
        {
            if (!string.IsNullOrEmpty(notif.ActionData))
            {
                var jObj = JObject.Parse(notif.ActionData);
                var entryId = jObj["event_id"]?.Value<string>();

                if (!string.IsNullOrEmpty(entryId))
                {
                    await Shell.Current.GoToAsync($"DetailPage?EntryId={entryId}");
                    // Popup schließen:
                    await MopupService.Instance.PopAsync();
                    return;
                }
            }

            // Fallback
            await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.EventLoadError);
        }

        public async Task MarkAllAsReadAsync()
        {
            try
            {
                var currentUserId = _supabaseClient.Auth.CurrentUser?.Id;
                if (string.IsNullOrEmpty(currentUserId))
                    return;

                // 1) Zuerst alle "read=false" Notifs des Users aus DB laden
                var readFalse = await _supabaseClient
                    .From<NotificationModel>()
                    .Filter("user_id", Operator.Equals, currentUserId)
                    .Filter("read", Operator.Equals, "false")
                    .Get();

                if (readFalse?.Models == null || readFalse.Models.Count == 0)
                {
                    Debug.WriteLine("[MarkAllAsReadAsync] Keine ungelesenen Benachrichtigungen gefunden.");
                    return;
                }

                // 2) Jeden Datensatz einzeln aktualisieren:
                //    Wir setzen `notif.Read = true` und rufen dann .Update(...) 
                //    mit dem einzelnen NotificationModel auf.
                foreach (var notif in readFalse.Models)
                {
                    notif.Read = true;

                    // Achtung: Bei .Update(model) muss Supabase wissen, 
                    //         welches Feld der Primary Key ist (z.B. "Id").
                    //         Sonst klappt die Zuordnung in der DB nicht.
                    await _supabaseClient
                        .From<NotificationModel>()
                        .Update(notif);
                }

                // 3) Lokal in deinem ObservableCollection-Cache (Notifications) 
                //    ebenfalls alle betroffenen auf Read=true setzen – 
                //    damit das UI sofort den geänderten Status anzeigt.
                foreach (var updatedNotif in readFalse.Models)
                {
                    var local = Notifications.FirstOrDefault(n => n.Id == updatedNotif.Id);
                    if (local != null)
                        local.Read = true;
                }
                _notificationService.RaiseNotificationsUpdated();
                Debug.WriteLine($"[MarkAllAsReadAsync] {readFalse.Models.Count} Benachrichtigungen einzeln auf 'read=true' gesetzt.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MarkAllAsReadAsync] Fehler: {ex}");
            }
        }
    }
}
