using Microsoft.Extensions.Logging;

namespace NBH.Services
{
    public class AppSessionFlowService
    {
        private readonly AuthService _authService;
        private readonly NotificationService _notificationService;
        private readonly AppWarmupService _appWarmupService;
        private readonly INavigationService _navigationService;
        private readonly NavigationOrchestrator _navigationOrchestrator;
        private readonly ILogger<AppSessionFlowService> _logger;

        public AppSessionFlowService(
            AuthService authService,
            NotificationService notificationService,
            AppWarmupService appWarmupService,
            INavigationService navigationService,
            NavigationOrchestrator navigationOrchestrator,
            ILogger<AppSessionFlowService> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _appWarmupService = appWarmupService ?? throw new ArgumentNullException(nameof(appWarmupService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _navigationOrchestrator = navigationOrchestrator ?? throw new ArgumentNullException(nameof(navigationOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> PrepareStartupAsync()
        {
            bool valid = await _authService.EnsureSessionValidAsync();
            if (valid)
            {
                // Warmup bewusst nicht-blockierend starten, damit Splash und Navigation
                // nicht von langsamen Netzwerkpfaden ausgebremst werden.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _appWarmupService.WarmAuthenticatedAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Nicht-blockierendes Warmup fehlgeschlagen.");
                    }
                });
            }

            return valid;
        }

        public async Task<bool> CompleteStartupAsync(bool sessionValid, bool navigateToLastPage, bool showSessionExpiredToast)
        {
            if (!sessionValid)
            {
                await NavigateToLoginAsync(
                    showSessionExpiredToast ? UserFriendlyErrors.Messages.SessionExpired : null);
                return false;
            }

            await _navigationOrchestrator.ShowAppAsync();

            if (navigateToLastPage)
            {
                await _navigationService.NavigateToLastPageAsync();
            }
            else
            {
                await _navigationService.GoToAsync(Routes.ProfilePage);
            }

            return true;
        }

        public async Task<bool> NavigateForCurrentSessionAsync(bool navigateToLastPage, bool showSessionExpiredToast)
        {
            var valid = await PrepareStartupAsync();
            return await CompleteStartupAsync(valid, navigateToLastPage, showSessionExpiredToast);
        }

        public async Task HandleLoginSuccessAsync()
        {
            _logger.LogInformation("Fuehre Login-Navigationsfluss aus.");
            _ = Task.Run(async () =>
            {
                try
                {
                    await _appWarmupService.WarmAuthenticatedAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Warmup nach Login fehlgeschlagen.");
                }
            });
            await _navigationOrchestrator.ShowAppAsync();
            await _navigationService.GoToAsync(Routes.ProfilePage);
        }

        public async Task HandleInactivityTimeoutAsync()
        {
            _logger.LogInformation("Sitzung wegen Inaktivitaet abgelaufen.");
            await _authService.LogoutAndClearSessionAsync();
            await NavigateToLoginAsync("Sitzung wegen Inaktivitaet abgelaufen.", showWarningToast: true);
        }

        public async Task NavigateToLoginAsync(string? message = null, bool showWarningToast = false)
        {
            _appWarmupService.Clear();
            await _navigationOrchestrator.ShowLoginAsync();

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (showWarningToast)
            {
                await ToastService.ShowWarningAsync(message);
            }
            else
            {
                await ToastService.ShowInfoAsync(message);
            }
        }
    }
}
