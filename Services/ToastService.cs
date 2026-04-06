using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

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
        // --------------------------------------------------------
        // ERFOLG: Gruener Snackbar (3s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowSuccessAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#2E7D32"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // FEHLER: Roter Snackbar (5s sichtbar, laenger weil wichtig)
        // --------------------------------------------------------
        public static async Task ShowErrorAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(5),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#C62828"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // INFO: Anthrazit Snackbar (3s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowInfoAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#4b4b4b"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
        }

        // --------------------------------------------------------
        // WARNUNG: Orange Snackbar (4s sichtbar)
        // --------------------------------------------------------
        public static async Task ShowWarningAsync(string message)
        {
            var snackbar = Snackbar.Make(
                message,
                duration: TimeSpan.FromSeconds(4),
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Color.FromArgb("#E65100"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Font = Microsoft.Maui.Font.SystemFontOfSize(15),
                    CharacterSpacing = 0.3
                });
            await snackbar.Show();
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
    }
}
