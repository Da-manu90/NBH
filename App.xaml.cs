using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using NBH.Services;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core;
using OneSignalSDK.DotNet.Core.Debug;
using System.Web;
using NBH.Resources.Models;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;

namespace NBH
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        private static PhoenixRealtimeService _realtimeService;
        private readonly AuthService _authService;

        public static IServiceProvider Services { get; private set; }
        private DateTime? _sleepTime;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _serviceProvider = serviceProvider;
            Services = serviceProvider;

            // Globale Exception-Handler für unbehandelte Exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Debug.WriteLine($"[UNHANDLED] {ex?.Message}\n{ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Debug.WriteLine($"[UNOBSERVED TASK] {e.Exception?.Message}");
                e.SetObserved(); // Verhindert App-Crash
            };

            OneSignal.Debug.LogLevel = LogLevel.VERBOSE;
            OneSignal.Initialize(AppConfig.OneSignalAppId);
            OneSignal.Notifications.RequestPermissionAsync(false);
            OneSignal.Notifications.Clicked += (sender, e) => 
            {
                var data = e.Notification.AdditionalData;
                if (data != null && data.ContainsKey("event_id"))
                {
                    var eventId = data["event_id"].ToString();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (Application.Current.MainPage is not AppShell)
                            Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                        await Shell.Current.GoToAsync($"detail?entryId={eventId}");
                    });
                }
            };

            // Hier holst du den RealtimeService und AuthService über den DI-Container:
            _realtimeService = _serviceProvider.GetRequiredService<PhoenixRealtimeService>();
            _authService = _serviceProvider.GetRequiredService<AuthService>();

            // **AB- dann AN-Melden** (verhindert doppelte Listener)
            _realtimeService.PostgresChanged -= Realtime_PostgresChanged;
            _realtimeService.PostgresChanged += Realtime_PostgresChanged;

            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                RemoveSafeAreaPadding();
            }
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            // Die MainPage wird explizit gesetzt, um den Fehler zu beheben.
            if (MainPage == null)
                MainPage = new SplashPage();

            return new Window(MainPage);
        }


        private void RemoveSafeAreaPadding()
        {
            if (MainPage is not null)
            {
                MainPage.Padding = new Thickness(0);
            }
        }


        protected override void OnStart()
        {
            base.OnStart();

            // SplashPage NUR beim erstmaligen App-Start setzen
            if (!Preferences.ContainsKey("HasLaunchedBefore"))
            {
                MainPage = new SplashPage();
                Preferences.Set("HasLaunchedBefore", true);
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _sleepTime = DateTime.UtcNow;
            Debug.WriteLine($"[App.xaml.cs] OnSleep: Zeit gespeichert ({_sleepTime})");
        }

        protected override async void OnResume()
        {
            base.OnResume();
            Debug.WriteLine("[App.xaml.cs] OnResume gestartet.");

            if (Preferences.Get("JustCameFromAppleLogin", false))
            {
                Preferences.Set("JustCameFromAppleLogin", false);
                Console.WriteLine("[App.xaml.cs] OnResume => Apple-Login-Flow => breche Session-Check ab.");
                return;
            }

            try
            {
                // Inaktivitäts-Timeout prüfen (30 Minuten)
                if (_sleepTime.HasValue)
                {
                    var timeInBg = DateTime.UtcNow - _sleepTime.Value;
                    if (timeInBg.TotalMinutes > 30)
                    {
                        Debug.WriteLine($"[App.xaml.cs] Inaktivität erkannt ({timeInBg.TotalMinutes:F1} Min). Logout erzwungen.");
                        _sleepTime = null;
                        await _authService.LogoutAndClearSessionAsync();
                        MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                        await Shell.Current.GoToAsync("//LoginPage");
                        await ToastService.ShowWarningAsync("Sitzung wegen Inaktivität abgelaufen.");
                        return;
                    }
                }
                _sleepTime = null; // Zurücksetzen nach erfolgreichem Resume

                Debug.WriteLine("[App.xaml.cs] Prüfe Session-Validität...");
                bool valid = await _authService.EnsureSessionValidAsync();

                if (!valid)
                {
                    Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                    await Shell.Current.GoToAsync("//LoginPage");
                    await ToastService.ShowInfoAsync(UserFriendlyErrors.Messages.SessionExpired);
                }
                else
                {
                    var notifService = MauiProgram.Services.GetRequiredService<NotificationService>();
                    await notifService.InitializeAsync();
                    Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                    await Shell.Current.GoToAsync(Preferences.Get("LastPage", "//MainTabs/ProfileTab/ProfilePage"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App.xaml.cs] Fehler in OnResume: {ex.Message}");
                Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
        public static PhoenixRealtimeService GetPhoenixRealtimeService() => _realtimeService;

        public IServiceProvider GetServiceProvider() => _serviceProvider;

        public async Task SubscribeEntriesRealtimeAsync(Guid currentUserId)
        {
            try
            {
                await _realtimeService.ConnectAsync();

                var userToken = ""; // oder await _authService.GetUserAccessTokenAsync();

                // 1) Abo für Einträge, die ich selbst erstellt habe
                _realtimeService.SubscribeChannel(
                    topic: "realtime:public:entries",
                    userToken: userToken,
                    schema: "public",
                    table: "entries",
                    filter: $"user_id=eq.{currentUserId}"
                );

                // 2) Abo für Einträge, bei denen ich der event_partner bin
                _realtimeService.SubscribeChannel(
                    topic: "realtime:public:entries",
                    userToken: userToken,
                    schema: "public",
                    table: "entries",
                    filter: $"event_partner_id=eq.{currentUserId}"
                );

                Console.WriteLine($"[App.xaml.cs] Realtime ABO auf entries: user_id={currentUserId} ODER event_partner_id={currentUserId} eingerichtet.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App.xaml.cs] Fehler: {ex.Message}");
            }
        }

        private void Realtime_PostgresChanged(object sender, PhoenixPostgresEvent e)
        {
            // Nur interessieren wir uns für "entries" 
            if (e.Table == "entries")
            {
                WeakReferenceMessenger.Default.Send(e, "EntriesChanged");
            }
        }

        protected override async void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            Console.WriteLine($"[App.xaml.cs] OnAppLinkRequestReceived => {uri}");

            // DeeplinkHandler verwenden (zwingend!)
            var deeplinkHandler = Services.GetRequiredService<DeeplinkHandler>();
            await deeplinkHandler.HandleDeeplinkAsync(uri);

            // --------------------------------------------------------------------------------
            // 1) Prüfen, ob es Apple OAuth Callback ist:
            // --------------------------------------------------------------------------------
            if (uri.Host == "auth" && (uri.LocalPath == "/callback" || uri.AbsolutePath == "/callback"))
            {
                Console.WriteLine("[App.xaml.cs] -> Apple OAuth Callback erkannt.");

                // NEU: Aus dem Querystring Tokens abgreifen!
                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
                
                // --- SECURITY FIX: State-Validierung ---
                var returnedState = qs["state"];
                var savedState = await SecureStorage.Default.GetAsync("oauth_state");
                if (string.IsNullOrEmpty(returnedState) || returnedState != savedState)
                {
                    Console.WriteLine("[App.xaml.cs] SECURITY ERROR: State mismatch! Apple Login abgelehnt.");
                    await ToastService.ShowErrorAsync("Sicherheitsprüfung für Login fehlgeschlagen.");
                    return;
                }
                SecureStorage.Default.Remove("oauth_state"); // Verbraucht

                var accessToken = qs["access_token"];
                var refreshToken = qs["refresh_token"];

                // Nur wenn wir echte Tokens haben, können wir die Session setzen
                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    var authService = Services.GetRequiredService<AuthService>();

                    // Session mit den Tokens setzen
                    await authService.SetSessionAsync(accessToken, refreshToken);

                    // Kleine Pause (optional)
                    await Task.Delay(500);
                }
                else
                {
                    Console.WriteLine("[App.xaml.cs] Keine access_token/refresh_token Parameter gefunden.");
                }

                // Jetzt erst Session checken
                var authSrv = Services.GetRequiredService<AuthService>();
                bool isValid = await authSrv.EnsureSessionValidAsync();
                if (isValid)
                {
                    Console.WriteLine("[App.xaml.cs] => Session valid => Weiter zur Hauptseite");

                    // *** NEU: NotificationService aufrufen ***
                    var notifService = Services.GetRequiredService<NotificationService>();
                    await notifService.InitializeAsync();
                    Console.WriteLine("[App.xaml.cs] => NotificationService init done (SocialLogin).");

                    // NEUE ZEILE: Flag setzen, damit OnResume() NICHT erneut Login erfordert
                    Preferences.Set("JustCameFromAppleLogin", true);

                    if (Application.Current.MainPage is not AppShell)
                        Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();

                    await Shell.Current.GoToAsync("//MainTabs/ProfileTab/ProfilePage");
                }
                else
                {
                    Console.WriteLine("[App.xaml.cs] => Session NICHT valid => LoginPage");

                    if (Application.Current.MainPage is not AppShell)
                        Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();

                    await Shell.Current.GoToAsync("//LoginPage");
                }

                // Link bereits verarbeitet => return
                return;
            }

            // --------------------------------------------------------------------------------
            // 2) Prüfen, ob es ein "detailpage"-Deeplink ist: myapp://detailpage?entryId=...
            // --------------------------------------------------------------------------------
            if (uri.Host == "detailpage")
            {
                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var entryId = qs["entryId"];

                if (!string.IsNullOrEmpty(entryId))
                {
                    Console.WriteLine($"[App.xaml.cs] Deeplink zur DetailPage erkannt, entryId={entryId}");

                    if (Application.Current.MainPage is not AppShell)
                        Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();

                    await Shell.Current.GoToAsync($"detail?entryId={entryId}");
                }
                else
                {
                    Console.WriteLine("[App.xaml.cs] Deeplink 'detailpage' erkannt, aber keine entryId vorhanden.");
                }
            }
        }
    }
}





