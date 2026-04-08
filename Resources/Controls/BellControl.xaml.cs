using Mopups.Services;
using System.Diagnostics;
using NBH.Resources.Controls;
using NBH.Resources.Views;
using NBH.Resources.ViewModels;
using NBH.Resources.Models;
using NBH.Services;
using Microsoft.Extensions.Logging;

namespace NBH.Resources.Controls
{
    public partial class BellControl : ContentView
    {
        private readonly NotificationService _notificationService;
        private int _unreadCount;

        public BellControl()
        {
            InitializeComponent();

            // **KORRIGIERT**: Nicht nur 'var notificationService', sondern das FELD '_notificationService' befÃƒÆ’Ã‚Â¼llen!
            var serviceProvider = ((App)Application.Current!).GetServiceProvider();
            _notificationService = serviceProvider.GetService(typeof(NotificationService)) as NotificationService ?? throw new InvalidOperationException("NotificationService not registered.");
            _notificationService.NotificationsUpdated -= OnNotificationsUpdated;
            _notificationService.NotificationsUpdated += OnNotificationsUpdated;
            _notificationService.NewNotificationReceived -= NotificationService_NewNotificationReceived;
            _notificationService.NewNotificationReceived += NotificationService_NewNotificationReceived;
        }

        // Benannte Methode fÃƒÆ’Ã‚Â¼rs Event (kein anonymes Lambda):
        private void NotificationService_NewNotificationReceived(object sender, NotificationModel notif)
        {
            // Du kannst auch direkt 'await UpdateBadgeAsync();' im Task-Kontext machen
            OnNewNotificationReceived();
        }

        private async void OnNotificationsUpdated(object sender, EventArgs e)
        {
            // Dein Badge aktualisieren
            await UpdateBadgeAsync();
        }

        // Methode, wenn neue Notification reinkommt
        public async void OnNewNotificationReceived()
        {
            // 1) Badge aktualisieren
            await UpdateBadgeAsync();
            // 2) Glocken-GIF abspielen
            HapticFeedbackHelper.PerformNotificationFeedback();
#if IOS
            NBH.Platforms.iOS.iOSHapticFeedback.TriggerLightImpact();
#endif
            await PlayBellGifOnce();
        }

        public async Task UpdateBadgeAsync()
        {
            // --> Hier war _notificationService null => Crash
            var unreadCount = _notificationService.Notifications.Count(n => !n.Read);

            _unreadCount = unreadCount;

            NotificationBadgeLabel.Text = _unreadCount.ToString();
            NotificationBadgeFrame.IsVisible = (_unreadCount > 0);
        }

        private async Task PlayBellGifOnce()
        {
            try
            {
                BellGif.IsVisible = true;
                BellButton.IsVisible = false;

                await Task.Delay(6000);

                BellGif.IsVisible = false;
                BellButton.IsVisible = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BellControl] Fehler bei PlayBellGifOnce: " + ex.Message);
            }
        }

        private async void OnBellClicked(object sender, EventArgs e)
        {
            // 1) Bounce-Animation
            if (sender is VisualElement element)
            {
                await element.ScaleTo(0.8, 80, Easing.CubicIn);
                await element.ScaleTo(1.25, 80, Easing.CubicOut);
                await element.ScaleTo(1.0, 50, Easing.Linear);
            }

            // 2) Haptic Feedback
#if IOS
            NBH.Platforms.iOS.iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // 3) Debug-Output oder Mopups
            Debug.WriteLine("[BellControl] Glocke geklickt -> zeige Popup");

            var serviceProvider = ((App)Application.Current!).GetServiceProvider();
            var vm = serviceProvider.GetService(typeof(NotificationListViewModel)) as NotificationListViewModel
                ?? throw new InvalidOperationException("NotificationListViewModel not registered.");

            var popup = new NotificationPopup(vm);


            await MopupService.Instance.PushAsync(popup);
        }
    }
}
