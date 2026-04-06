// ============================================================
// MauiProgram.cs Ã¢â‚¬â€œ Professionelle Version (ANGEPASST)
// ============================================================

using CommunityToolkit.Maui;
using FFImageLoading.Maui;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;
using NBH.Resources.ViewModels;
using NBH.Services;
using NBH.Resources.Models;
using Supabase;
using NBH.Resources.Controls;
using System.Diagnostics;
using Indiko.Maui.Controls.Markdown;

namespace NBH
{
    public static class MauiProgram
    {
        // ============================================================
        // TEMPORÃƒâ€žR: Diese Property bleibt, bis ALLE Pages auf DI
        // umgestellt sind. Danach entfernen (Schritt 9 im Umbauplan).
        // ============================================================
        public static IServiceProvider Services { get; private set; }

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiMaps()
                .ConfigureMopups()
                .UseFFImageLoading()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf",  "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("verdanai.ttf",          "VerdanaItalic");
                    fonts.AddFont("verdanaz.ttf",          "VerdanaBoldItalic");
                    fonts.AddFont("verdanab.ttf",          "VerdanaBold");
                    fonts.AddFont("verdana.ttf",           "Verdana");
                });

            builder.ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Shell, NBH.Platforms.Android.CustomShellHandler>();
#elif IOS
                handlers.AddHandler<Shell, NBH.Platforms.iOS.CustomShellHandler>();
#endif
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            // Registrierungen in saubere Gruppen aufteilen
            RegisterInfrastructure(builder.Services);
            RegisterServices(builder.Services);
            RegisterViewModels(builder.Services);
            RegisterPages(builder.Services);
            RegisterRoutes();

            var app = builder.Build();

            ConfigureAppCenter();

            // TEMPORÃƒâ€žR Ã¢â‚¬â€œ wird in Schritt 9 entfernt
            Services = app.Services;

            return app;
        }

        // ============================================================
        // Infrastructure: Supabase-Client, Navigation, Realtime
        // ============================================================
        private static void RegisterInfrastructure(IServiceCollection services)
        {
            var supabaseClient = new Supabase.Client(
                AppConfig.SupabaseUrl,
                AppConfig.SupabaseAnonKey,
                new SupabaseOptions
                {
                    AutoConnectRealtime = false,
                    SessionHandler = new SupabaseSessionHandler()
                }
            );
            services.AddSingleton(supabaseClient);

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // PhoenixRealtimeService mit dynamischer URL
            services.AddSingleton<PhoenixRealtimeService>(provider =>
                new PhoenixRealtimeService(
                    AppConfig.SupabaseUrl,
                    AppConfig.SupabaseAnonKey,
                    provider.GetRequiredService<AuthService>()
                )
            );
        }

        // ============================================================
        // Services: Auth, Notifications, Push, Deeplinks
        // ============================================================
        private static void RegisterServices(IServiceCollection services)
        {
            // AuthService OHNE supabaseApiKey (Profi-Version)
            services.AddSingleton<AuthService>(sp =>
            {
                var authService = new AuthService(
                    sp.GetRequiredService<Supabase.Client>(),
                    sp.GetRequiredService<ILogger<AuthService>>()
                );
                Task.Run(async () => await authService.InitializeAsync());
                return authService;
            });

            services.AddSingleton<NotificationService>();
            services.AddSingleton<MyPushDelegate>();
            services.AddTransient<DeeplinkHandler>();
            services.AddSingleton<FeedbackService>();
            services.AddSingleton<ModerationService>();
        }

        // ============================================================
        // ViewModels
        // ============================================================
        private static void RegisterViewModels(IServiceCollection services)
        {
            // LoginPageViewModel (Profi: AuthService, NotificationService, INavigationService, Logger)
            services.AddTransient<LoginPageViewModel>(sp => new LoginPageViewModel(
                sp.GetRequiredService<AuthService>(),
                sp.GetRequiredService<NotificationService>(),
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ILogger<LoginPageViewModel>>()
            ));

            // RegisterPageViewModel (Profi: AuthService, INavigationService, Logger)
            services.AddSingleton<RegisterPageViewModel>(sp => new RegisterPageViewModel(
                sp.GetRequiredService<AuthService>(),
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ILogger<RegisterPageViewModel>>()
            ));

            // Bestehende ViewModels
            services.AddTransient<ProfilePageViewModel>();
            services.AddTransient<DetailPageViewModel>();
            services.AddTransient<AddPageViewModel>();
            services.AddTransient<FAQPageViewModel>();
            services.AddTransient<UserProfilePageViewModel>();
            services.AddTransient<CreateEventPageViewModel>();
            services.AddTransient<SettingsPageViewModel>();

            // ChatPageViewModel (spezielle Konstruktor-Signatur)
            services.AddTransient<ChatPageViewModel>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ChatPageViewModel>>();
                var authService = sp.GetRequiredService<AuthService>();
                var supabaseClient = sp.GetRequiredService<Supabase.Client>();
                var phoenixSrv = sp.GetRequiredService<PhoenixRealtimeService>();
                return new ChatPageViewModel(logger, authService, supabaseClient, phoenixSrv);
            });

            // RatingPageViewModel hat eigenen Konstruktor mit nur Client
            services.AddTransient<RatingPageViewModel>(sp =>
                new RatingPageViewModel(sp.GetRequiredService<Client>()));

            // ChatViewPageViewModel: Transient (frische Daten bei jedem Besuch)
            services.AddTransient<ChatViewPageViewModel>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ChatViewPageViewModel>>();
                var auth = sp.GetRequiredService<AuthService>();
                var client = sp.GetRequiredService<Client>();
                return new ChatViewPageViewModel(logger, auth, client);
            });
        }

        // ============================================================
        // Pages
        // ============================================================
        private static void RegisterPages(IServiceCollection services)
        {
            // Singletons (nur globale Shell-Elemente)
            services.AddSingleton<AppShell>();
            services.AddSingleton<SplashPage>();
            services.AddSingleton<RegisterPage>();

            // Transients (frische Instanz bei jedem Besuch)
            services.AddTransient<ProfilePage>();
            services.AddTransient<ChatViewPage>();
            services.AddTransient<RatingPage>();
            services.AddTransient<LoginPage>();
            services.AddTransient<DetailPage>();
            services.AddTransient<AddPage>();
            services.AddTransient<FAQPage>();
            services.AddTransient<UserProfilePage>();
            services.AddTransient<CropImagePage>();
            services.AddTransient<CreateEventPage>();
            services.AddTransient<SettingsPage>();
        }

        // ============================================================
        // Routes: Alle Shell-Routen registrieren
        // ============================================================
        private static void RegisterRoutes()
        {
            Routing.RegisterRoute(nameof(CropImagePage), typeof(CropImagePage));
            Routing.RegisterRoute(nameof(DetailPage),    typeof(DetailPage));
            Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
            Routing.RegisterRoute(nameof(ChatPage),      typeof(ChatPage));
            Routing.RegisterRoute(nameof(RatingPage),    typeof(RatingPage));
            Routing.RegisterRoute(nameof(CreateEventPage), typeof(CreateEventPage));
        }

        // ============================================================
        // AppCenter
        // ============================================================
        private static void ConfigureAppCenter()
        {
            AppCenter.Start(
                $"android={AppConfig.AppCenterAndroid};ios={AppConfig.AppCenteriOS}",
                typeof(Analytics),
                typeof(Crashes)
            );
        }
    }
}



