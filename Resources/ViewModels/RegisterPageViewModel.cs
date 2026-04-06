// ============================================================
// RegisterPageViewModel.cs â€“ Professionelle Version
// ============================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NBH.Services;
using System.Text.RegularExpressions;

namespace NBH.Resources.ViewModels
{
    public partial class RegisterPageViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly ILogger<RegisterPageViewModel> _logger;

        // --------------------------------------------------------
        // Properties
        // --------------------------------------------------------

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string _username = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string _password = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string _confirmPassword = string.Empty;

        [ObservableProperty] private string _errorMessage  = string.Empty;
        [ObservableProperty] private bool   _isErrorVisible;
        [ObservableProperty] private bool   _isSuccessVisible;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool _hasAcceptedTerms;

        [ObservableProperty]
        private bool _showTermsError;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private bool _isBusy;

        // --------------------------------------------------------
        // Konstruktor
        // --------------------------------------------------------

        public RegisterPageViewModel(
            AuthService authService,
            INavigationService navigationService,
            ILogger<RegisterPageViewModel> logger)
        {
            _authService       = authService       ?? throw new ArgumentNullException(nameof(authService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _logger            = logger            ?? throw new ArgumentNullException(nameof(logger));
        }

        // --------------------------------------------------------
        // Commands
        // --------------------------------------------------------

        private bool CanRegister() =>
            !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Email)
            && !string.IsNullOrWhiteSpace(Password)
            && Password.Length >= 8
            && HasAcceptedTerms;

        [RelayCommand]
        private async Task OpenTermsAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/terms", BrowserLaunchMode.SystemPreferred);
        }

        [RelayCommand]
        private async Task OpenPrivacyAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/privacy", BrowserLaunchMode.SystemPreferred);
        }

        [RelayCommand(CanExecute = nameof(CanRegister))]
        private async Task RegisterAsync()
        {
            HideError();

            if (!ValidateInput(out string validationError))
            {
                ShowError(validationError);
                return;
            }

            IsBusy = true;

            try
            {
                bool success = await _authService.SignUpUser(Username, Email, Password);

                if (success)
                {
                    _logger.LogInformation(
                        "Registrierung erfolgreich fÃ¼r {Email}.", Email);

                    await ToastService.ShowSuccessAsync("Registrierung erfolgreich! Bitte melde dich jetzt an.");
                    await Task.Delay(1000);

                    await _navigationService.GoToAsync(Routes.LoginPage);
                }
                else
                {
                    _logger.LogWarning(
                        "Registrierung fehlgeschlagen fÃ¼r {Email}.", Email);

                    ShowError("Registrierung fehlgeschlagen. MÃ¶glicherweise ist diese E-Mail bereits registriert.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unerwarteter Fehler bei der Registrierung.");
                ShowError("Ein unerwarteter Fehler ist aufgetreten. Bitte spÃ¤ter erneut versuchen.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task NavigateToLoginAsync()
        {
            await _navigationService.GoToAsync(Routes.LoginPage);
        }

        // --------------------------------------------------------
        // Validierung
        // --------------------------------------------------------

        private bool ValidateInput(out string error)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                error = "Bitte einen Benutzernamen eingeben.";
                return false;
            }

            if (Username.Length < 3 || Username.Length > 30)
            {
                error = "Der Benutzername muss zwischen 3 und 30 Zeichen lang sein.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                error = "Bitte eine E-Mail-Adresse eingeben.";
                return false;
            }

            if (!IsValidEmail(Email))
            {
                error = "Bitte eine gÃ¼ltige E-Mail-Adresse eingeben.";
                return false;
            }

            if (Password.Length < 8)
            {
                error = "Das Passwort muss mindestens 8 Zeichen lang sein.";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                error = "Die PasswÃ¶rter stimmen nicht Ã¼berein.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidEmail(string email)
        {
            return Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase);
        }

        private void ShowError(string message)
        {
            ErrorMessage    = message;
            IsErrorVisible  = true;
            IsSuccessVisible = false;
        }

        private void HideError()
        {
            ErrorMessage    = string.Empty;
            IsErrorVisible  = false;
        }
    }
}

