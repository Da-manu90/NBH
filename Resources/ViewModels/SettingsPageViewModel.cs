using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using NBH.Services;

#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly Supabase.Client _supabaseClient;
        private readonly FeedbackService _feedbackService;

        public SettingsPageViewModel(AuthService authService, Supabase.Client supabaseClient, FeedbackService feedbackService)
        {
            _authService = authService;
            _supabaseClient = supabaseClient;
            _feedbackService = feedbackService;

            // Lade Einstellungen
            PushNotificationsEnabled = Preferences.Get("push_enabled", true);
            EmailNotificationsEnabled = Preferences.Get("email_enabled", true);
            NearbyEventsEnabled = Preferences.Get("nearby_enabled", true);
            var isDark = Preferences.Get("dark_mode", false);
            _isDarkModeEnabled = isDark;
            
            CurrentUserEmail = _authService.GetCurrentUserEmail() ?? "Nicht eingeloggt";
            CurrentUsername = "Max Mustermann"; // TODO: Aus DB lesen in echtem System oder Session
            IsVerified = true;
            IsProfilePublic = true;
        }

        public string AppVersion => $"Version {AppInfo.VersionString} (Build {AppInfo.BuildString})";

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string currentUserEmail;

        [ObservableProperty]
        private string currentUsername;

        [ObservableProperty]
        private bool isVerified;

        [ObservableProperty]
        private bool isProfilePublic;

        private bool _pushNotificationsEnabled;
        public bool PushNotificationsEnabled
        {
            get => _pushNotificationsEnabled;
            set
            {
                SetProperty(ref _pushNotificationsEnabled, value);
                Preferences.Set("push_enabled", value);

                // Haptic feedback
#if IOS
                iOSHapticFeedback.TriggerLightImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
        }

        private bool _emailNotificationsEnabled;
        public bool EmailNotificationsEnabled
        {
            get => _emailNotificationsEnabled;
            set
            {
                SetProperty(ref _emailNotificationsEnabled, value);
                Preferences.Set("email_enabled", value);

                // Haptic feedback
#if IOS
                iOSHapticFeedback.TriggerLightImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
        }

        private bool _nearbyEventsEnabled;
        public bool NearbyEventsEnabled
        {
            get => _nearbyEventsEnabled;
            set
            {
                SetProperty(ref _nearbyEventsEnabled, value);
                Preferences.Set("nearby_enabled", value);

                // Haptic feedback
#if IOS
                iOSHapticFeedback.TriggerLightImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
            }
        }

        private bool _isDarkModeEnabled;
        public bool IsDarkModeEnabled
        {
            get => _isDarkModeEnabled;
            set
            {
                if (SetProperty(ref _isDarkModeEnabled, value))
                {
                    ExecuteToggleDarkMode();
                }
            }
        }

        private void ExecuteToggleDarkMode()
        {
            Application.Current.UserAppTheme = IsDarkModeEnabled ? AppTheme.Dark : AppTheme.Light;
            Preferences.Set("dark_mode", IsDarkModeEnabled);

            // Haptic feedback
#if IOS
            iOSHapticFeedback.TriggerLightImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
        }

        [RelayCommand]
        private async Task OpenPrivacyPolicyAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/privacy", BrowserLaunchMode.SystemPreferred);
        }

        [RelayCommand]
        private async Task OpenTermsAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/terms", BrowserLaunchMode.SystemPreferred);
        }

        [RelayCommand]
        private async Task NavigateToFaqAsync()
        {
            // Shell.Current.GoToAsync(Routes.FaqPage); // Assuming FaqPage exists
        }

        [RelayCommand]
        private async Task OpenFeedbackAsync()
        {
            // Start simple feedback process
            var emailMsg = new EmailMessage
            {
                Subject = "App Feedback / Support",
                To = new List<string> { "support@nbh-app.de" }
            };
            await Email.ComposeAsync(emailMsg);
        }

        [RelayCommand]
        private async Task OpenAppStoreRatingAsync()
        {
            // AppReviewRequest mechanism or fall back to URL
            await Browser.OpenAsync("https://play.google.com/store/apps/details?id=companyname.nbh", BrowserLaunchMode.SystemPreferred);
        }

        [RelayCommand]
        private async Task ChangePasswordAsync()
        {
            if (string.IsNullOrEmpty(CurrentUserEmail) || CurrentUserEmail == "Nicht eingeloggt") return;

            bool success = await _authService.SendPasswordResetEmailAsync(CurrentUserEmail);
            if (success)
            {
                await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.PasswordResetSent);
            }
            else
            {
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.PasswordResetError);
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            // Schritt 1: Warnung
            var proceed = await Shell.Current.DisplayAlert(
                "Account löschen",
                "Wenn du deinen Account löschst, werden alle deine Daten " +
                "unwiderruflich entfernt: Profil, Einträge, Chats und Bewertungen.\n\n" +
                "Du hast 30 Tage Zeit, dies rückgängig zu machen.",
                "Weiter", "Abbrechen");

            if (!proceed) return;

            // Schritt 2: Passwort-Bestätigung
            var password = await Shell.Current.DisplayPromptAsync(
                "Bestätigung",
                "Gib dein Passwort ein um die Löschung zu bestätigen:",
                placeholder: "Passwort",
                maxLength: 100,
                keyboard: Microsoft.Maui.Keyboard.Text);

            if (string.IsNullOrEmpty(password)) return;

            // Passwort validieren via Re-Authentifizierung
            var isValid = await _authService.ValidatePasswordAsync(password);
            if (!isValid)
            {
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.WrongPassword);
                return;
            }

            IsBusy = true;
            try
            {
                // Löschung vormerken
                var currentUserId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId)) return;

                var uid = Guid.Parse(currentUserId);
                await _supabaseClient.From<NBH.Resources.Models.User>()
                    .Where(u => u.Id == uid)
                    .Set(u => u.DeletionRequestedAt, DateTime.UtcNow)
                    .Set(u => u.DeletionScheduledAt, DateTime.UtcNow.AddDays(30))
                    .Update();

                // Ausloggen
                await _supabaseClient.Auth.SignOut();
                await Shell.Current.GoToAsync("//LoginPage");

                // Erfolgs-Toast nach Navigation
                await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.AccountDeleteSuccess);
            }
            catch (Exception ex)
            {
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.AccountDeleteError);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

