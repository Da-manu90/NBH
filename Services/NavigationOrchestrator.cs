using Microsoft.Maui.Controls;

namespace NBH.Services
{
    public class NavigationOrchestrator
    {
        private readonly AppShell _appShell;

        public NavigationOrchestrator(AppShell appShell)
        {
            _appShell = appShell ?? throw new ArgumentNullException(nameof(appShell));
        }

        public async Task<AppShell> ShowAppAsync()
        {
            return await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Application.Current?.Windows.Count > 0)
                {
                    var currentPage = Application.Current.Windows[0].Page;
                    if (!ReferenceEquals(currentPage, _appShell))
                    {
                        Application.Current.Windows[0].Page = _appShell;
                    }
                }

                return _appShell;
            });
        }

        public async Task ShowLoginAsync()
        {
            await ShowAppAsync();

            if (Shell.Current?.CurrentState?.Location?.OriginalString != Routes.LoginPage)
            {
                await Shell.Current.GoToAsync(Routes.LoginPage);
            }
        }
    }
}
