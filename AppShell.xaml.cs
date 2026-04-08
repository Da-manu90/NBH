using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using NBH.Services;
using NBH.Resources.Views;
using Mopups.Services;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class AppShell : Shell
    {
        private readonly AuthService _authService;


        // ↑ Falls du direkt das AuthService brauchst, 
        //   kannst du es per Konstruktor-Injection oder ServiceLocator holen.

        public AppShell(AuthService authService)
        {
            _authService = authService;
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                foreach (var item in Shell.Current.Items)
                {
                    Debug.WriteLine("[AppShell] Route: " + item.Route);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppShell] Fehler beim Zugriff auf Shell-Routen: " + ex.Message);
            }
        }

        protected override async void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);

            var currentUri = args.Current?.Location?.ToString() ?? string.Empty;
            Debug.WriteLine($"[AppShell] OnNavigated => {currentUri}");

            Preferences.Set("LastPage", currentUri);

            // (Beispiel) Apple Login Callback
            if (currentUri.Contains("auth/callback"))
            {
                bool isAuth = await _authService.EnsureSessionValidAsync();
                if (isAuth)
                    await Shell.Current.GoToAsync(Routes.ProfilePage);
                else
                    await Shell.Current.GoToAsync(Routes.LoginPage);
            }
        }
    }
}

