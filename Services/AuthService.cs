// ============================================================
// AuthService.cs â€“ Professionelle Version
// ============================================================

using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using Supabase;

// Ã„NDERUNG 1: System.Net.Http, AuthenticationHeaderValue, Encoding,
// Newtonsoft.Json und Microsoft.Maui.Storage (SecureStorage) â€“ alles
// komplett entfernt. Warum das mÃ¶glich ist, erklÃ¤re ich unten.

namespace NBH.Services
{
    // ============================================================
    // ANALYSE: Was war falsch?
    //
    // Problem A â€“ HttpClient als Singleton-Instanz mit Clear()
    // -------------------------------------------------------
    // Das Original hat einen HttpClient im Konstruktor erstellt
    // (_httpClient = new HttpClient()) und ihn dann in mehreren
    // Methoden mit _httpClient.DefaultRequestHeaders.Clear()
    // genutzt. Das ist ein Race Condition-Bug:
    //
    // DefaultRequestHeaders sind NICHT thread-safe. Wenn zwei
    // Tasks gleichzeitig Clear() + Add() aufrufen (z.B. Login
    // und Username-Check parallel), kÃ¶nnen Headers verschwinden
    // oder Ã¼berschrieben werden, ohne dass eine Exception kommt.
    // Der Request schlÃ¤gt dann still fehl.
    //
    // Die LÃ¶sung: Den Supabase SDK verwenden, der Headers intern
    // korrekt verwaltet. Kein manueller HttpClient nÃ¶tig.
    //
    // Problem B â€“ Manuelle SecureStorage-Verwaltung
    // ---------------------------------------------
    // SecureStorage.SetAsync(SessionStorageKey, ...) stand an
    // 8 verschiedenen Stellen im Code. Jede Methode hat die
    // Session nach ihrer Operation selbst gespeichert. Das fÃ¼hrt
    // zu Inkonsistenzen: ValidateAndRefreshSessionAsync speichert
    // einen leeren String bei Fehler, EnsureSessionValidAsync
    // macht dasselbe, aber mit anderem Verhalten.
    //
    // Die LÃ¶sung: SupabaseSessionHandler in MauiProgram.cs
    // Ã¼bernimmt das Speichern komplett. Der AuthService muss
    // sich nicht mehr darum kÃ¼mmern â€“ er macht Auth, nicht Storage.
    //
    // Problem C â€“ REST/v1 direkt aufrufen statt SDK
    // ---------------------------------------------
    // IsUsernameValid, SignUpUser und UpdateUserProfileAsync haben
    // raw HTTP-Requests gebaut ($"{SupabaseUrl}/rest/v1/users...").
    // Das Supabase SDK hat dafÃ¼r typsichere Methoden:
    //   _supabaseClient.From<User>().Where(...).Get()
    // Diese sind kÃ¼rzer, sicherer und URL-unabhÃ¤ngig.
    //
    // Problem D â€“ Doppelte Methoden
    // -----------------------------
    // ValidateAndRefreshSessionAsync und EnsureSessionValidAsync
    // taten dasselbe mit leicht unterschiedlichem Verhalten.
    // LoadSessionFromSecureStorageAsync und der SecureStorage-Block
    // in EnsureSessionValidAsync waren ebenfalls doppelt.
    // Resultat: Wenn man einen Bug in der Session-Logik fixen
    // wollte, musste man 3 Stellen gleichzeitig Ã¤ndern.
    //
    // Problem E â€“ Initialisierung mehrfach aufrufbar
    // -----------------------------------------------
    // InitializeAsync konnte beliebig oft aufgerufen werden.
    // Im Original passierte das 3 Mal beim App-Start. Ohne
    // einen Guard lÃ¤uft der Supabase-Client mehrfach an.
    // ============================================================

    public class AuthService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<AuthService> _logger;

        // Ã„NDERUNG 2: _supabaseApiKey und _httpClient komplett
        // entfernt. Der SDK braucht den Key nicht nach auÃŸen,
        // und raw HTTP-Calls werden durch SDK-Calls ersetzt.

        // Ã„NDERUNG 3: _isInitialized als Guard gegen Mehrfach-
        // Initialisierung. Ein SemaphoreSlim(1,1) stellt sicher,
        // dass auch bei parallelen Aufrufen nur einer durchkommt.
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public AuthService(
            Supabase.Client supabaseClient,
            ILogger<AuthService> logger)
        {
            // Ã„NDERUNG 4: Kein 'supabaseApiKey'-Parameter mehr.
            // Der Key steckt bereits im Client, den MauiProgram.cs
            // erstellt. AuthService braucht ihn nicht nochmal.
            _supabaseClient = supabaseClient
                ?? throw new ArgumentNullException(nameof(supabaseClient));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        // --------------------------------------------------------
        // Initialisierung
        // --------------------------------------------------------

        public async Task InitializeAsync()
        {
            // Ã„NDERUNG 5: SemaphoreSlim verhindert parallele
            // Initialisierung. WaitAsync(0) gibt sofort false zurÃ¼ck
            // wenn bereits jemand wartet, ohne zu blockieren.
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return; // Double-checked nach dem Lock

                await _supabaseClient.InitializeAsync();
                _isInitialized = true;
                _logger.LogInformation("Supabase-Client erfolgreich initialisiert.");

                // Ã„NDERUNG 6: LoadSessionFromSecureStorageAsync entfernt.
                // Der SupabaseSessionHandler in MauiProgram.cs lÃ¤dt die
                // Session automatisch beim Erstellen des Clients.
                // Diese Methode war redundant.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Initialisierung des Supabase-Clients.");
                throw; // Re-throw: Initialisierungsfehler sollen nicht verschluckt werden
            }
            finally
            {
                _initLock.Release();
            }
        }

        // --------------------------------------------------------
        // Auth-Status
        // --------------------------------------------------------

        // Ã„NDERUNG 7: IsAuthenticatedAsync â†’ IsAuthenticated (synchron).
        // Die Methode liest nur eine Property â€“ kein await, kein Netzwerk.
        // Ein 'Async'-Suffix zu vergeben, nur weil Task.FromResult()
        // benutzt wird, ist irrefÃ¼hrend. Aufrufer wÃ¼rden einen await
        // schreiben, der technisch nie wartet.
        public bool IsAuthenticated =>
            _supabaseClient.Auth.CurrentSession != null;

        // Ã„NDERUNG 8: GetCurrentUserIdAsync â†’ GetCurrentUserId (synchron)
        // Selbe BegrÃ¼ndung: kein Netzwerkzugriff, kein await nÃ¶tig.
        public string GetCurrentUserId() =>
            _supabaseClient.Auth.CurrentUser?.Id;

        public string GetCurrentUserEmail() =>
            _supabaseClient.Auth.CurrentUser?.Email;

        // --------------------------------------------------------
        // Login
        // --------------------------------------------------------

        public async Task<bool> LoginUser(string email, string password)
        {
            try
            {
                var result = await _supabaseClient.Auth.SignIn(email, password);

                if (result?.User == null)
                {
                    _logger.LogWarning("Login fehlgeschlagen â€“ keine User-Daten zurÃ¼ckgegeben.");
                    return false;
                }

                // Ã„NDERUNG 9: Kein manuelles SecureStorage.SetAsync mehr.
                // SupabaseSessionHandler speichert die Session automatisch
                // nachdem SignIn() intern die Session setzt.
                _logger.LogInformation(
                    "Login erfolgreich fÃ¼r {Email}.", result.User.Email);
                return true;
            }
            catch (Exception ex)
            {
                // Ã„NDERUNG 10: Structured Logging statt String-Interpolation.
                // _logger.LogError(ex, $"Fehler: {ex.Message}") ist doppelt â€“
                // die Exception enthÃ¤lt bereits die Message. Und Interpolation
                // verhindert, dass Log-Systeme (AppCenter, Serilog) die
                // Parameter separat indizieren kÃ¶nnen.
                _logger.LogError(ex, "Fehler beim Login fÃ¼r {Email}.", email);
                return false;
            }
        }

        /// <summary>
        /// Validiert das Passwort des aktuell angemeldeten Nutzers,
        /// indem ein erneuter Login-Versuch unternommen wird (Re-Authentifizierung).
        /// </summary>
        public async Task<bool> ValidatePasswordAsync(string password)
        {
            var email = GetCurrentUserEmail();
            if (string.IsNullOrEmpty(email)) return false;

            try
            {
                // Wir nutzen SignIn, um die Gültigkeit des Passworts zu prüfen.
                // Supabase behält die Session bei oder aktualisiert sie.
                var result = await _supabaseClient.Auth.SignIn(email, password);
                return result?.User != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Passwort-Validierung fehlgeschlagen für {Email}.", email);
                return false;
            }
        }

        // --------------------------------------------------------
        // Registrierung
        // --------------------------------------------------------

        public async Task<bool> SignUpUser(string username, string email, string password)
        {
            try
            {
                var result = await _supabaseClient.Auth.SignUp(email, password);

                if (result?.User == null)
                {
                    _logger.LogWarning(
                        "Registrierung fehlgeschlagen fÃ¼r {Email} â€“ kein User zurÃ¼ckgegeben.", email);
                    return false;
                }

                var userId = Guid.Parse(result.User.Id);
                _logger.LogInformation(
                    "Auth-Registrierung erfolgreich: {Email}, Id={UserId}.", email, userId);

                // Ã„NDERUNG 11: Kein raw HttpClient mehr. Das Supabase SDK
                // macht denselben Insert typsicher, ohne manuelle Headers,
                // ohne URL-Strings und ohne JSON-Serialisierung.
                await _supabaseClient
                    .From<User>()
                    .Insert(new User
                    {
                        Id        = userId,
                        Username  = username,
                        Email     = email,
                        Role      = "user",
                        CreatedAt = DateTime.UtcNow,
                        TermsAcceptedAt = DateTime.UtcNow,
                        PrivacyAcceptedAt = DateTime.UtcNow,
                        TermsVersion = "1.0"
                    });

                _logger.LogInformation(
                    "DB-Eintrag erfolgreich erstellt fÃ¼r {Username}.", username);

                // Ã„NDERUNG 12: Kein SecureStorage.SetAsync â€“ SupabaseSessionHandler
                // Ã¼bernimmt das nach dem SignUp automatisch.
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Registrierung fÃ¼r {Email}.", email);
                return false;
            }
        }

        // Ã„NDERUNG 13: IsUsernameValid â†’ IsUsernameAvailableAsync
        // (besserer Name â€“ "valid" ist mehrdeutig, "available" ist klar)
        // Kein raw HttpClient mehr, SDK-Abfrage stattdessen.
        public async Task<bool> IsUsernameAvailableAsync(string username)
        {
            try
            {
                var response = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Username == username)
                    .Get()
                    .ConfigureAwait(false);

                bool available = response.Models.Count == 0;

                _logger.LogInformation(
                    "Username '{Username}' verfÃ¼gbar: {Available}.", username, available);

                return available;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei IsUsernameAvailableAsync fÃ¼r '{Username}'.", username);
                return false; // Im Zweifel: nicht verfÃ¼gbar (sicherer)
            }
        }

        // --------------------------------------------------------
        // Session-Verwaltung
        // --------------------------------------------------------

        // Ã„NDERUNG 14: ValidateAndRefreshSessionAsync entfernt.
        // Es war ein Duplikat von EnsureSessionValidAsync mit
        // leicht anderem Verhalten. Eine Methode fÃ¼r eine Aufgabe.
        public async Task<bool> EnsureSessionValidAsync()
        {
            try
            {
                var session = _supabaseClient.Auth.CurrentSession;

                if (session == null)
                {
                    // Ã„NDERUNG 15: Kein manuelles SecureStorage lesen.
                    // Der SupabaseSessionHandler hat die Session bereits
                    // beim App-Start in den Client geladen. Wenn hier
                    // null zurÃ¼ckkommt, gibt es schlicht keine Session.
                    _logger.LogInformation("Keine aktive Session vorhanden.");
                    return false;
                }

                if (!session.Expired())
                {
                    // Ban-Check: Prüfe ob der User gesperrt ist
                    try
                    {
                        var currentUserId = GetCurrentUserId();
                        if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var uid))
                        {
                            var userResp = await _supabaseClient.From<NBH.Resources.Models.User>()
                                .Where(u => u.Id == uid)
                                .Get();
                            var user = userResp.Models?.FirstOrDefault();
                            if (user != null && user.IsBanned)
                            {
                                _logger.LogWarning("Nutzer {UserId} ist gesperrt. Abmeldung.", currentUserId);
                                await _supabaseClient.Auth.SignOut();
                                return false;
                            }
                        }
                    }
                    catch (Exception banEx)
                    {
                        _logger.LogWarning(banEx, "Ban-Check Fehler (nicht kritisch).");
                    }
                    return true;
                }

                _logger.LogInformation("Session abgelaufen â€“ versuche Refresh.");
                var refreshed = await _supabaseClient.Auth.RefreshSession();

                if (refreshed != null && !refreshed.Expired())
                {
                    // SupabaseSessionHandler speichert automatisch
                    _logger.LogInformation("Session erfolgreich aktualisiert.");
                    return true;
                }

                _logger.LogWarning("Session-Refresh fehlgeschlagen.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in EnsureSessionValidAsync.");
                return false;
            }
        }

        public async Task<string> GetUserAccessTokenAsync()
        {
            bool valid = await EnsureSessionValidAsync();
            if (!valid)
            {
                _logger.LogWarning("Kein gÃ¼ltiges AccessToken verfÃ¼gbar.");
                return null;
            }
            return _supabaseClient.Auth.CurrentSession?.AccessToken;
        }

        public async Task SetSessionAsync(string accessToken, string refreshToken)
        {
            try
            {
                await _supabaseClient.Auth.SetSession(accessToken, refreshToken, false);
                // Ã„NDERUNG 16: Kein manuelles SecureStorage.SetAsync.
                // SupabaseSessionHandler reagiert auf das SetSession()-Event
                // und speichert automatisch.
                _logger.LogInformation("Session erfolgreich gesetzt.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in SetSessionAsync.");
            }
        }

        public async Task LogoutAndClearSessionAsync()
        {
            try
            {
                await _supabaseClient.Auth.SignOut();
                // Ã„NDERUNG 17: Kein manuelles SecureStorage.Remove.
                // SupabaseSessionHandler ruft DestroySession() auf,
                // wenn SignOut() die Session im Client lÃ¶scht.
                // Nur Legacy-Keys aufrÃ¤umen, die der Handler nicht kennt:
                SecureStorage.Remove("jwt_token");

                _logger.LogInformation("Benutzer abgemeldet.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei LogoutAndClearSessionAsync.");
            }
        }

        // --------------------------------------------------------
        // Profil-Updates
        // --------------------------------------------------------

        // Ã„NDERUNG 18: Kein raw HttpClient mehr. Supabase SDK.
        public async Task<bool> UpdateUserProfileAsync(string userId, string profileImageUrl)
        {
            try
            {
                var userGuid = Guid.Parse(userId);
                await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Set(u => u.ProfileImageUrl, profileImageUrl)
                    .Update();

                _logger.LogInformation(
                    "Profilbild aktualisiert fÃ¼r UserId {UserId}.", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler beim Aktualisieren des Profilbilds fÃ¼r UserId {UserId}.", userId);
                return false;
            }
        }

        // Ã„NDERUNG 19: Neue Methode â€“ aus dem LoginPageViewModel hierher
        // verschoben. Datenbankoperationen gehÃ¶ren in Services, nicht
        // in ViewModels. Das war der AuslÃ¶ser fÃ¼r diese Refaktorierung.
        public async Task UpdateOneSignalSubscriptionAsync(string subscriptionId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "UpdateOneSignalSubscription: kein eingeloggter User.");
                return;
            }

            try
            {
                var userGuid = Guid.Parse(userId);

                // Nur aktualisieren, wenn sich die ID geÃ¤ndert hat
                var existing = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Get();

                var user = existing.Models.FirstOrDefault();
                if (user == null || user.SubscriptionId == subscriptionId)
                    return;

                await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Set(u => u.SubscriptionId, subscriptionId)
                    .Update();

                _logger.LogInformation(
                    "OneSignal SubscriptionId aktualisiert fÃ¼r UserId {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler beim Aktualisieren der OneSignal SubscriptionId.");
            }
        }

        // --------------------------------------------------------
        // Passwort-Reset
        // --------------------------------------------------------

        public async Task<bool> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                await _supabaseClient.Auth.ResetPasswordForEmail(email);
                _logger.LogInformation(
                    "Passwort-Reset-E-Mail versandt an {Email}.", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler beim Senden der Passwort-Reset-E-Mail an {Email}.", email);
                return false;
            }
        }

        // --------------------------------------------------------
        // Ã„NDERUNG 20: GetCurrentSessionAsync und GetApiKey entfernt.
        //
        // GetCurrentSessionAsync: Hat EnsureSessionValidAsync aufgerufen
        // und dann die Session zurÃ¼ckgegeben. Aufrufer kÃ¶nnen das selbst
        // tun (erst EnsureSessionValidAsync, dann GetUserAccessTokenAsync).
        // Eine Methode, die zwei andere aufruft nur um das Ergebnis
        // durchzureichen, ist unnÃ¶tige KomplexitÃ¤t.
        //
        // GetApiKey: Gibt den rohen API-Key nach auÃŸen. Andere Klassen
        // sollten den Key nicht kennen. PhoenixRealtimeService bekommt
        // seinen Key direkt beim Erstellen in MauiProgram.cs.
        // --------------------------------------------------------
    }
}


