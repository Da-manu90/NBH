using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Mopups.Services;
using NBH.Resources.Views;

namespace NBH.Services
{
    /// <summary>
    /// Globaler Toast/Snackbar-Service fuer einheitliche, nicht-blockierende Benachrichtigungen.
    /// Ersetzt die meisten DisplayAlert-Aufrufe durch moderne Snackbar-Nachrichten.
    ///
    /// Verwendung:
    ///   await ToastService.ShowSuccessAsync("Profil gespeichert!");
    ///   await ToastService.ShowErrorAsync("Verbindung fehlgeschlagen.");
    ///   await ToastService.ShowInfoAsync("Keine Aenderungen erkannt.");
    /// </summary>
    public static class ToastService
    {
        private static readonly SemaphoreSlim _queue = new(1, 1);

        // --------------------------------------------------------
        // ERFOLG: Gruener Snackbar (3s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowSuccessAsync(string message)
        {
            HapticFeedbackHelper.PerformSuccessFeedback();
            await ShowBannerAsync(StatusBannerType.Success, message, TimeSpan.FromSeconds(5));
        }

        // --------------------------------------------------------
        // FEHLER: Roter Snackbar (5s sichtbar, laenger weil wichtig)
        // --------------------------------------------------------
        public static async Task ShowErrorAsync(string message)
        {
            HapticFeedbackHelper.PerformErrorFeedback();
            await ShowBannerAsync(StatusBannerType.Error, message, TimeSpan.FromSeconds(7));
        }

        // --------------------------------------------------------
        // INFO: Anthrazit Snackbar (3s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowInfoAsync(string message)
        {
            HapticFeedbackHelper.PerformClickFeedback();
            await ShowBannerAsync(StatusBannerType.Info, message, TimeSpan.FromSeconds(5));
        }

        // --------------------------------------------------------
        // WARNUNG: Orange Snackbar (4s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowWarningAsync(string message)
        {
            HapticFeedbackHelper.PerformWarningFeedback();
            await ShowBannerAsync(StatusBannerType.Warning, message, TimeSpan.FromSeconds(6));
        }

        // --------------------------------------------------------
        // SNACKBAR MIT AKTION: z.B. "Chat archiviert" + "Rueckgaengig"
        // --------------------------------------------------------
        public static async Task ShowWithActionAsync(
            string message,
            string actionText,
            Action onAction)
        {
            var snackbar = Snackbar.Make(
                message,
                actionButtonText: actionText,
                action: () => onAction?.Invoke(),
                duration: TimeSpan.FromSeconds(5),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#4b4b4b"),
                    TextColor = Colors.White,
                    ActionButtonTextColor = Color.FromArgb("#00d2a8"),
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                });
            await snackbar.Show();
        }

        private static async Task ShowBannerAsync(StatusBannerType type, string message, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            await _queue.WaitAsync();
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        var popup = new StatusBannerPopup(type, message, duration);
                        await MopupService.Instance.PushAsync(popup);
                        await popup.WaitForDismissalAsync();
                    }
                    catch
                    {
                        await ShowSnackbarFallbackAsync(type, message, duration);
                    }
                });
            }
            finally
            {
                _queue.Release();
            }
        }

        private static async Task ShowSnackbarFallbackAsync(StatusBannerType type, string message, TimeSpan duration)
        {
            var background = type switch
            {
                StatusBannerType.Success => Color.FromArgb("#1FA34A"),
                StatusBannerType.Warning => Color.FromArgb("#F97316"),
                StatusBannerType.Error => Color.FromArgb("#D92D20"),
                _ => Color.FromArgb("#4B4B4B")
            };

            var snackbar = Snackbar.Make(
                message,
                actionButtonText: "OK",
                action: () => { },
                duration: duration,
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = background,
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                    CharacterSpacing = 0.3
                });

            await snackbar.Show();
        }
    }
}
