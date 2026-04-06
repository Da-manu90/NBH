// ============================================================
// LoginPageViewModel.cs – Professionelle Version
// ============================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NBH.Services;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core.User;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public partial class LoginPageViewModel : ObservableObject
    {
        private readonly AuthService            _authService;
        private readonly NotificationService    _notificationService;
        private readonly INavigationService     _navigationService;
        private readonly ILogger<LoginPageViewModel> _logger;

        private readonly Dictionary<string, (int Attempts, DateTime LockoutEnd)>
            _loginAttempts = new();

        // --------------------------------------------------------
        // Properties mit [ObservableProperty]
        // --------------------------------------------------------

        [ObservableProperty] private string _email    = string.Empty;
        [ObservableProperty] private string _password = string.Empty;

        [ObservableProperty] private string _errorMessage  = string.Empty;
        [ObservableProperty] private bool   _isErrorVisible;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
        private bool _isBusy;

        // --------------------------------------------------------
        // Konstruktor
        // --------------------------------------------------------

        public LoginPageViewModel(
            AuthService authService,
            NotificationService notificationService,
            INavigationService navigationService,
            ILogger<LoginPageViewModel> logger)
        {
            _authService         = authService         ?? throw new ArgumentNullException(nameof(authService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _navigationService   = navigationService   ?? throw new ArgumentNullException(nameof(navigationService));
            _logger              = logger              ?? throw new ArgumentNullException(nameof(logger));
        }

        // --------------------------------------------------------
        // Commands
        // --------------------------------------------------------

        private bool CanSignIn() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSignIn))]
        private async Task SignInAsync()
        {
            if (!ValidateLoginInput())
                return;

            if (!CheckRateLimit())
                return;

            IsBusy = true;
            HideError();

            try
            {
                bool success = await _authService.LoginUser(Email, Password);

                if (success)
                {
                    await OnLoginSuccessAsync();
                }
                else
                {
                    RecordFailedAttempt();
                    ShowError("Login fehlgeschlagen. Bitte E-Mail und Passwort prüfen.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unerwarteter Fehler beim Login.");
                ShowError("Ein unerwarteter Fehler ist aufgetreten. Bitte später erneut versuchen.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToRegisterAsync()
        {
            await _navigationService.GoToAsync(nameof(RegisterPage));
        }

        [RelayCommand]
        private async Task SocialLoginAsync(string provider)
        {
            if (string.IsNullOrEmpty(provider)) provider = "apple";

            IsBusy = true;
            HideError();

            try
            {
                var state = Guid.NewGuid().ToString();
                await SecureStorage.Default.SetAsync("oauth_state", state);

                var providerUrl = $"{AppConfig.SupabaseUrl}/auth/v1/authorize"
                    + $"?provider={provider.ToLower()}"
                    + "&redirect_to=myapp://auth/callback"
                    + "&flow_type=pkce"
                    + "&scope=openid+email+profile"
                    + $"&state={state}";

                _logger.LogInformation($"Starte {provider} Social Login.");
                await Launcher.OpenAsync(new Uri(providerUrl));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fehler beim Social Login mit {provider}.");
                ShowError($"{provider} Login fehlgeschlagen. Bitte später erneut versuchen.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // --------------------------------------------------------
        // Private Hilfsmethoden
        // --------------------------------------------------------

        private async Task OnLoginSuccessAsync()
        {
            _logger.LogInformation("Login erfolgreich.");
            _loginAttempts.Remove(Email);

            await _notificationService.InitializeAsync();
            await SyncOneSignalSubscriptionAsync();

            await ToastService.ShowSuccessAsync("Login erfolgreich! Willkommen zurÃ¼ck.");
            await Task.Delay(800); // Kurze Pause damit der User den Toast sieht

            await _navigationService.GoToAsync(Routes.ProfilePage);

#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#else
            HapticFeedback.Perform(HapticFeedbackType.Click);
#endif
        }

        private bool ValidateLoginInput()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ShowError("E-Mail und Passwort dürfen nicht leer sein.");
                HapticFeedback.Perform(HapticFeedbackType.Click);
                return false;
            }
            return true;
        }

        private bool CheckRateLimit()
        {
            if (!_loginAttempts.TryGetValue(Email, out var data))
                return true;

            if (DateTime.UtcNow < data.LockoutEnd)
            {
                var remaining = (data.LockoutEnd - DateTime.UtcNow).Minutes + 1;
                ShowError($"Zu viele Fehlversuche. Bitte warte noch {remaining} Minute(n).");
                return false;
            }

            return true;
        }

        private void RecordFailedAttempt()
        {
            var current = _loginAttempts.GetValueOrDefault(Email);
            var newAttempts = current.Attempts + 1;

            var lockoutEnd = newAttempts >= 5
                ? DateTime.UtcNow.AddMinutes(5)
                : DateTime.UtcNow;

            _loginAttempts[Email] = (newAttempts, lockoutEnd);

            _logger.LogWarning(
                "Fehlgeschlagener Login-Versuch {Attempt} für {Email}.",
                newAttempts, Email);
        }

        private async Task SyncOneSignalSubscriptionAsync()
        {
            try
            {
                var subscriptionId = OneSignal.User.PushSubscription?.Id;
                if (string.IsNullOrEmpty(subscriptionId))
                    return;

                await _authService.UpdateOneSignalSubscriptionAsync(subscriptionId);

                OneSignal.User.Changed -= OnOneSignalUserChanged;
                OneSignal.User.Changed += OnOneSignalUserChanged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OneSignal-Subscription konnte nicht gespeichert werden.");
            }
        }

        private void OnOneSignalUserChanged(object? sender, UserStateChangedEventArgs e)
        {
            var subscriptionId = OneSignal.User.PushSubscription?.Id;
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                _ = _authService.UpdateOneSignalSubscriptionAsync(subscriptionId);
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage   = message;
            IsErrorVisible = true;
        }

        private void HideError()
        {
            ErrorMessage   = string.Empty;
            IsErrorVisible = false;
        }
    }
}
