// ============================================================
// NavigationService.cs – Erweitert mit GoToAsync()
// ============================================================
// ÄNDERUNGEN:
// 1. GoToAsync(string route) implementiert – generische Navigation
// 2. Legacy-Methoden nutzen jetzt intern GoToAsync + Routes-Konstanten
// 3. CropImageStream bleibt als static Property (wird von CropImagePage genutzt)
// ============================================================

using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace NBH.Services
{
    public class NavigationService : INavigationService
    {
        /// <summary>
        /// Statischer Stream für das Zuschneiden von Bildern.
        /// Wird von CropImagePage gelesen.
        /// </summary>
        public static Stream CropImageStream { get; set; }

        /// <summary>
        /// Gibt an, in welchem Kontext die CropImagePage genutzt wird (z.B. "Profile" oder "Event").
        /// </summary>
        public static string ImageCropMode { get; set; } = "Profile";

        // ----------------------------------------------------------
        // NEUE generische Methode
        // ----------------------------------------------------------

        public async Task GoToAsync(string route)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Sicherstellen, dass eine Shell existiert
                if (Shell.Current == null)
                {
                    Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                }

                await Shell.Current.GoToAsync(route);
            });
        }

        // ----------------------------------------------------------
        // Legacy-Methoden – nutzen jetzt intern GoToAsync + Routes
        // ----------------------------------------------------------

        public async Task NavigateToMainPageAsync()
        {
            await GoToAsync(Routes.ProfilePage);
        }

        public async Task NavigateToRegisterPageAsync()
        {
            // RegisterPage ist relative Route (Push-Navigation)
            await Shell.Current.GoToAsync(Routes.RegisterPage);
        }

        public async Task NavigateBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        public async Task NavigateToCropImagePageAsync()
        {
            await Shell.Current.GoToAsync(Routes.CropImage);
        }

        public async Task NavigateToLoginPageAsync()
        {
            await GoToAsync(Routes.LoginPage);
        }

        public async Task NavigateToLastPageAsync()
        {
            var lastPage = Preferences.Get("LastPage", Routes.ProfilePage);
            await Shell.Current.GoToAsync(lastPage);
        }
    }
}
