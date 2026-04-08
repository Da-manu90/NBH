using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace NBH.Services
{
    public class NavigationService : INavigationService
    {
        private readonly NavigationOrchestrator _navigationOrchestrator;

        public static Stream CropImageStream { get; set; }

        public static string ImageCropMode { get; set; } = "Profile";

        public NavigationService(NavigationOrchestrator navigationOrchestrator)
        {
            _navigationOrchestrator = navigationOrchestrator ?? throw new ArgumentNullException(nameof(navigationOrchestrator));
        }

        public async Task GoToAsync(string route)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _navigationOrchestrator.ShowAppAsync();
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(route);
                }
            });
        }

        public async Task NavigateToMainPageAsync()
        {
            await GoToAsync(Routes.ProfilePage);
        }

        public async Task NavigateToRegisterPageAsync()
        {
            await GoToAsync(Routes.RegisterPage);
        }

        public async Task NavigateBackAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await _navigationOrchestrator.ShowAppAsync();
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("..");
                }
            });
        }

        public async Task NavigateToCropImagePageAsync()
        {
            await GoToAsync(Routes.CropImage);
        }

        public async Task NavigateToLoginPageAsync()
        {
            await GoToAsync(Routes.LoginPage);
        }

        public async Task NavigateToLastPageAsync()
        {
            var lastPage = Preferences.Get("LastPage", Routes.ProfilePage);
            await GoToAsync(lastPage);
        }
    }
}
