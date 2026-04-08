using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using NBH.Resources.Models;
using NBH.Services;
using OneSignalSDK.DotNet;
using OneSignalSDK.DotNet.Core;
using OneSignalSDK.DotNet.Core.Debug;

namespace NBH
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AppShell _appShell;
        private static PhoenixRealtimeService _realtimeService;
        private readonly AuthService _authService;
        private readonly AppSessionFlowService _appSessionFlowService;
        private readonly NavigationOrchestrator _navigationOrchestrator;

        public static IServiceProvider Services { get; private set; }
        private DateTime? _sleepTime;

        private Page? GetRootPage()
        {
            if (Windows.Count > 0)
            {
                return Windows[0].Page;
            }

            return null;
        }

        private void SetRootPage(Page page)
        {
            if (Windows.Count > 0)
            {
                Windows[0].Page = page;
            }
        }

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _serviceProvider = serviceProvider;
            Services = serviceProvider;

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Debug.WriteLine($"[UNHANDLED] {ex?.Message}\n{ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Debug.WriteLine($"[UNOBSERVED TASK] {e.Exception?.Message}");
                e.SetObserved();
            };

            OneSignal.Debug.LogLevel = LogLevel.VERBOSE;
            OneSignal.Initialize(AppConfig.OneSignalAppId);
            OneSignal.Notifications.RequestPermissionAsync(false);
            OneSignal.Notifications.Clicked += (sender, e) =>
            {
                var data = e.Notification.AdditionalData;
                if (data == null || !data.ContainsKey("event_id"))
                {
                    return;
                }

                var eventId = data["event_id"].ToString();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    EnsureShellMainPage();
                    await Shell.Current.GoToAsync($"{Routes.DetailPage}?entryId={eventId}");
                });
            };

            _realtimeService = _serviceProvider.GetRequiredService<PhoenixRealtimeService>();
            _authService = _serviceProvider.GetRequiredService<AuthService>();
            _appSessionFlowService = _serviceProvider.GetRequiredService<AppSessionFlowService>();
            _appShell = _serviceProvider.GetRequiredService<AppShell>();
            _navigationOrchestrator = _serviceProvider.GetRequiredService<NavigationOrchestrator>();

            _realtimeService.PostgresChanged -= Realtime_PostgresChanged;
            _realtimeService.PostgresChanged += Realtime_PostgresChanged;

            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                RemoveSafeAreaPadding();
            }
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var rootPage = GetRootPage() ?? _serviceProvider.GetRequiredService<SplashPage>();
            return new Window(rootPage);
        }

        private void RemoveSafeAreaPadding()
        {
            var rootPage = GetRootPage();
            if (rootPage is not null)
            {
                rootPage.Padding = new Thickness(0);
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (!Preferences.ContainsKey("HasLaunchedBefore"))
            {
                SetRootPage(_serviceProvider.GetRequiredService<SplashPage>());
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
            try
            {
                base.OnResume();
                Debug.WriteLine("[App.xaml.cs] OnResume gestartet.");

                if (Preferences.Get("JustCameFromAppleLogin", false))
                {
                    Preferences.Set("JustCameFromAppleLogin", false);
                    Debug.WriteLine("[App.xaml.cs] OnResume => Apple-Login-Flow => Session-Check ausgelassen.");
                    return;
                }

                if (_sleepTime.HasValue)
                {
                    var timeInBg = DateTime.UtcNow - _sleepTime.Value;
                    if (timeInBg.TotalMinutes > 30)
                    {
                        Debug.WriteLine($"[App.xaml.cs] Inaktivitaet erkannt ({timeInBg.TotalMinutes:F1} Min). Logout erzwungen.");
                        _sleepTime = null;
                        await _appSessionFlowService.HandleInactivityTimeoutAsync();
                        return;
                    }
                }

                _sleepTime = null;

                Debug.WriteLine("[App.xaml.cs] Pruefe Session-Validitaet...");
                await _appSessionFlowService.NavigateForCurrentSessionAsync(
                    navigateToLastPage: true,
                    showSessionExpiredToast: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] OnResume error: {ex}");
                await _appSessionFlowService.NavigateToLoginAsync();
            }
        }

        public static PhoenixRealtimeService GetPhoenixRealtimeService() => _realtimeService;

        public IServiceProvider GetServiceProvider() => _serviceProvider;

        private AppShell EnsureShellMainPage()
        {
            if (GetRootPage() is AppShell currentShell)
            {
                return currentShell;
            }

            SetRootPage(_appShell);
            return _appShell;
        }

        public async Task SubscribeEntriesRealtimeAsync(Guid currentUserId)
        {
            try
            {
                await _realtimeService.ConnectAsync();

                var userToken = string.Empty;

                _realtimeService.SubscribeChannel(
                    topic: "realtime:public:entries",
                    userToken: userToken,
                    schema: "public",
                    table: "entries",
                    filter: $"user_id=eq.{currentUserId}"
                );

                _realtimeService.SubscribeChannel(
                    topic: "realtime:public:entries",
                    userToken: userToken,
                    schema: "public",
                    table: "entries",
                    filter: $"event_partner_id=eq.{currentUserId}"
                );

                Debug.WriteLine($"[App.xaml.cs] Realtime-Abo auf entries fuer {currentUserId} eingerichtet.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App.xaml.cs] Fehler beim SubscribeEntriesRealtimeAsync: {ex.Message}");
            }
        }

        private void Realtime_PostgresChanged(object sender, PhoenixPostgresEvent e)
        {
            if (e.Table == "entries")
            {
                WeakReferenceMessenger.Default.Send(e, "EntriesChanged");
            }
        }

        protected override async void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);

            Debug.WriteLine($"[App.xaml.cs] OnAppLinkRequestReceived => {uri}");

            var deeplinkHandler = Services.GetRequiredService<DeeplinkHandler>();
            await deeplinkHandler.HandleDeeplinkAsync(uri);

            if (uri.Host == "auth" && (uri.LocalPath == "/callback" || uri.AbsolutePath == "/callback"))
            {
                Debug.WriteLine("[App.xaml.cs] Apple OAuth Callback erkannt.");

                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);

                var returnedState = qs["state"];
                var savedState = await SecureStorage.Default.GetAsync("oauth_state");
                if (string.IsNullOrEmpty(returnedState) || returnedState != savedState)
                {
                    Debug.WriteLine("[App.xaml.cs] SECURITY ERROR: State mismatch.");
                    await ToastService.ShowErrorAsync("Sicherheitspruefung fuer Login fehlgeschlagen.");
                    return;
                }

                SecureStorage.Default.Remove("oauth_state");

                var accessToken = qs["access_token"];
                var refreshToken = qs["refresh_token"];

                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    await _authService.SetSessionAsync(accessToken, refreshToken);
                    await Task.Delay(500);
                }
                else
                {
                    Debug.WriteLine("[App.xaml.cs] Keine access_token/refresh_token Parameter gefunden.");
                }

                bool isValid = await _appSessionFlowService.NavigateForCurrentSessionAsync(
                    navigateToLastPage: false,
                    showSessionExpiredToast: false);

                if (isValid)
                {
                    Debug.WriteLine("[App.xaml.cs] Session valid => Weiter zur Hauptseite");
                    Preferences.Set("JustCameFromAppleLogin", true);
                }
                else
                {
                    Debug.WriteLine("[App.xaml.cs] Session NICHT valid => LoginPage");
                }

                return;
            }

            if (uri.Host == "detailpage")
            {
                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var entryId = qs["entryId"];

                if (!string.IsNullOrEmpty(entryId))
                {
                    Debug.WriteLine($"[App.xaml.cs] Deeplink zur DetailPage erkannt, entryId={entryId}");
                    await _navigationOrchestrator.ShowAppAsync();
                    await Shell.Current.GoToAsync($"{Routes.DetailPage}?entryId={entryId}");
                }
                else
                {
                    Debug.WriteLine("[App.xaml.cs] Deeplink detailpage ohne entryId.");
                }
            }
        }
    }
}
