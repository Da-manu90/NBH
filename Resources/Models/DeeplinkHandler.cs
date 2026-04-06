using System;
using System.Threading.Tasks;
using NBH.Services;
using Supabase;

namespace NBH.Resources.Models
{
    public class DeeplinkHandler
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly INavigationService _navigationService;

        public DeeplinkHandler(Supabase.Client supabaseClient, INavigationService navigationService)
        {
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        }

        public async Task HandleDeeplinkAsync(Uri uri)
        {
            if (uri == null)
            {
                Console.WriteLine("Die erhaltene URI ist null.");
                return;
            }

            Console.WriteLine($"Deeplink empfangen: {uri}");

            if (uri.Host == "auth" && (uri.LocalPath == "/callback" || uri.AbsolutePath == "/callback"))
            {
                // Fragment-Teil extrahieren
                var uriFragment = uri.Fragment.TrimStart('#');
                var queryParams = System.Web.HttpUtility.ParseQueryString(uriFragment);

                var accessToken = queryParams["access_token"];
                var refreshToken = queryParams["refresh_token"];
                var expiresIn = queryParams["expires_in"];
                var tokenType = queryParams["token_type"];

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(expiresIn))
                {
                    Console.WriteLine("Fehlende Parameter im Deeplink.");
                    return;
                }

                try
                {
                    await _supabaseClient.Auth.SetSession(accessToken, refreshToken);
                    Console.WriteLine("Login erfolgreich. Navigiere zur Hauptseite.");

                    // Navigation über INavigationService statt LoginViewModel
                    await _navigationService.GoToAsync(Routes.ProfilePage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Verarbeiten des Deeplinks: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Unbekannter Deeplink: {uri}");
            }
        }
    }
}
