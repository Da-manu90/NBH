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

            // z.B. wenn du DI nutzt oder ServiceLocator:
            // AuthService via DI injiziert
            
            // Routen registrieren, damit du GoToAsync(nameof(...)) nutzen kannst
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(DetailPage), typeof(DetailPage));
            Routing.RegisterRoute(nameof(AddPage), typeof(AddPage));
            Routing.RegisterRoute(nameof(ChatViewPage), typeof(ChatViewPage));
            Routing.RegisterRoute(nameof(FAQPage), typeof(FAQPage));
            Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
            Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
            Routing.RegisterRoute(nameof(RatingPage), typeof(RatingPage));
            Routing.RegisterRoute(nameof(CreateEventPage), typeof(CreateEventPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

            Debug.WriteLine("[AppShell] Routen registriert.");
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
                    await Shell.Current.GoToAsync("//MainTabs/ProfileTab/ProfilePage");
                else
                    await Shell.Current.GoToAsync("//LoginPage");
            }
        }
    }
}

