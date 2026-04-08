using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using NBH.Resources.Controls;
using NBH.Services;
using Mopups.Services;
using Supabase;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System;
using System.Windows.Input;

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
        public ICommand OpenNotificationCommand { get; }

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
            OpenNotificationCommand = new AsyncRelayCommand<NotificationModel>(OnNotificationTappedAsync);
        }

        private async Task LoadNotificationsFromDatabaseAsync()
        {
            try
            {
                await _notificationService.ReloadForPopupAsync(unreadOnly: false);
                Debug.WriteLine($"[NotificationService] Notifications geladen: {Notifications.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Fehler beim Laden: {ex.Message}");
            }
        }

        // Notification-Aktion
        private async Task OnRatingAsync(NotificationModel? notif)
        {
            if (notif == null)
            {
                return;
            }

            var jObj = notif.ActionDataObject;
            if (jObj != null)
            {
                var chatId =
                    jObj["chat_id"]?.ToString()
                    ?? jObj["conversation_id"]?.ToString()
                    ?? jObj["thread_id"]?.ToString();

                if (!string.IsNullOrWhiteSpace(chatId))
                {
                    var encodedChatId = Uri.EscapeDataString(chatId);
                    // Navigate to ChatTab first, then push ChatPage
                    await NavigateFromPopupAsync(
                        Routes.ChatView,
                        $"{nameof(ChatPage)}?chatId={encodedChatId}");
                    return;
                }

                var entryId = jObj?["event_id"]?.ToString()
                    ?? jObj?["entry_id"]?.ToString();

                if (!string.IsNullOrEmpty(entryId))
                {
                    // Navigate to ProfileTab first, then push DetailPage
                    await NavigateFromPopupAsync(
                        Routes.ProfilePage,
                        $"{nameof(DetailPage)}?EntryId={Uri.EscapeDataString(entryId)}");
                    return;
                }
            }

            // Fallback
            await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.EventLoadError);
        }

        private async Task OnNotificationTappedAsync(NotificationModel? notif)
        {
            if (notif == null)
            {
                return;
            }

            await MarkNotificationAsReadAsync(notif);
            await OnRatingAsync(notif);
        }

        private static async Task NavigateFromPopupAsync(string tabRoute, string? pushRoute = null)
        {
            try
            {
                if (MopupService.Instance.PopupStack.Any())
                {
                    await MopupService.Instance.PopAsync();
                }
            }
            catch
            {
                // Ignore popup close errors, still attempt navigation.
            }

            // Small delay to let popup dismissal complete before Shell navigation
            await Task.Delay(150);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync(tabRoute);

                    if (!string.IsNullOrWhiteSpace(pushRoute))
                    {
                        // Small delay to let tab switch settle
                        await Task.Delay(100);
                        await Shell.Current.GoToAsync(pushRoute);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NotificationNav] Fehler: {ex.Message}");
                    try
                    {
                        await Shell.Current.GoToAsync(Routes.ChatView);
                    }
                    catch { }
                }
            });
        }

        private async Task MarkNotificationAsReadAsync(NotificationModel notif)
        {
            try
            {
                if (notif.Read)
                {
                    return;
                }

                var currentUserId = _supabaseClient.Auth.CurrentUser?.Id;
                if (string.IsNullOrWhiteSpace(currentUserId))
                {
                    return;
                }

                await _supabaseClient.From<NotificationModel>()
                    .Where(n => n.Id == notif.Id)
                    .Set(n => n.Read, true)
                    .Update();

                notif.Read = true;
                _notificationService.RaiseNotificationsUpdated();
            }
            catch
            {
                // no-op, navigation should still work
            }
        }

        public async Task MarkAllAsReadAsync()
        {
            try
            {
                var currentUserId = _supabaseClient.Auth.CurrentUser?.Id;
                if (string.IsNullOrEmpty(currentUserId))
                    return;

                var response = await _supabaseClient.Rpc("mark_notifications_read_self", new Dictionary<string, object>());
                var updatedCount = string.IsNullOrWhiteSpace(response?.Content)
                    ? 0
                    : Newtonsoft.Json.JsonConvert.DeserializeObject<int>(response.Content);

                if (updatedCount == 0)
                {
                    Debug.WriteLine("[MarkAllAsReadAsync] Keine ungelesenen Benachrichtigungen gefunden.");
                    return;
                }

                foreach (var local in Notifications.Where(n => !n.Read))
                    local.Read = true;

                _notificationService.RaiseNotificationsUpdated();
                Debug.WriteLine($"[MarkAllAsReadAsync] {updatedCount} Benachrichtigungen via RPC auf 'read=true' gesetzt.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MarkAllAsReadAsync] Fehler: {ex}");
            }
        }
    }
}
