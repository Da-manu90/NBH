// ============================================================
// AuthService.cs Ã¢â‚¬â€œ Professionelle Version
// ============================================================

using Microsoft.Extensions.Logging;
using NBH.Resources.Models;
using Supabase;

// Ãƒâ€žNDERUNG 1: System.Net.Http, AuthenticationHeaderValue, Encoding,
// Newtonsoft.Json und Microsoft.Maui.Storage (SecureStorage) Ã¢â‚¬â€œ alles
// komplett entfernt. Warum das mÃƒÂ¶glich ist, erklÃƒÂ¤re ich unten.

namespace NBH.Services
{
    // ============================================================
    // ANALYSE: Was war falsch?
    //
    // Problem A Ã¢â‚¬â€œ HttpClient als Singleton-Instanz mit Clear()
    // -------------------------------------------------------
    // Das Original hat einen HttpClient im Konstruktor erstellt
    // (_httpClient = new HttpClient()) und ihn dann in mehreren
    // Methoden mit _httpClient.DefaultRequestHeaders.Clear()
    // genutzt. Das ist ein Race Condition-Bug:
    //
    // DefaultRequestHeaders sind NICHT thread-safe. Wenn zwei
    // Tasks gleichzeitig Clear() + Add() aufrufen (z.B. Login
    // und Username-Check parallel), kÃƒÂ¶nnen Headers verschwinden
    // oder ÃƒÂ¼berschrieben werden, ohne dass eine Exception kommt.
    // Der Request schlÃƒÂ¤gt dann still fehl.
    //
    // Die LÃƒÂ¶sung: Den Supabase SDK verwenden, der Headers intern
    // korrekt verwaltet. Kein manueller HttpClient nÃƒÂ¶tig.
    //
    // Problem B Ã¢â‚¬â€œ Manuelle SecureStorage-Verwaltung
    // ---------------------------------------------
    // SecureStorage.SetAsync(SessionStorageKey, ...) stand an
    // 8 verschiedenen Stellen im Code. Jede Methode hat die
    // Session nach ihrer Operation selbst gespeichert. Das fÃƒÂ¼hrt
    // zu Inkonsistenzen: ValidateAndRefreshSessionAsync speichert
    // einen leeren String bei Fehler, EnsureSessionValidAsync
    // macht dasselbe, aber mit anderem Verhalten.
    //
    // Die LÃƒÂ¶sung: SupabaseSessionHandler in MauiProgram.cs
    // ÃƒÂ¼bernimmt das Speichern komplett. Der AuthService muss
    // sich nicht mehr darum kÃƒÂ¼mmern Ã¢â‚¬â€œ er macht Auth, nicht Storage.
    //
    // Problem C Ã¢â‚¬â€œ REST/v1 direkt aufrufen statt SDK
    // ---------------------------------------------
    // IsUsernameValid, SignUpUser und UpdateUserProfileAsync haben
    // raw HTTP-Requests gebaut ($"{SupabaseUrl}/rest/v1/users...").
    // Das Supabase SDK hat dafÃƒÂ¼r typsichere Methoden:
    //   _supabaseClient.From<User>().Where(...).Get()
    // Diese sind kÃƒÂ¼rzer, sicherer und URL-unabhÃƒÂ¤ngig.
    //
    // Problem D Ã¢â‚¬â€œ Doppelte Methoden
    // -----------------------------
    // ValidateAndRefreshSessionAsync und EnsureSessionValidAsync
    // taten dasselbe mit leicht unterschiedlichem Verhalten.
    // LoadSessionFromSecureStorageAsync und der SecureStorage-Block
    // in EnsureSessionValidAsync waren ebenfalls doppelt.
    // Resultat: Wenn man einen Bug in der Session-Logik fixen
    // wollte, musste man 3 Stellen gleichzeitig ÃƒÂ¤ndern.
    //
    // Problem E Ã¢â‚¬â€œ Initialisierung mehrfach aufrufbar
    // -----------------------------------------------
    // InitializeAsync konnte beliebig oft aufgerufen werden.
    // Im Original passierte das 3 Mal beim App-Start. Ohne
    // einen Guard lÃƒÂ¤uft der Supabase-Client mehrfach an.
    // ============================================================

    public class AuthService
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<AuthService> _logger;

        // Ãƒâ€žNDERUNG 2: _supabaseApiKey und _httpClient komplett
        // entfernt. Der SDK braucht den Key nicht nach auÃƒÅ¸en,
        // und raw HTTP-Calls werden durch SDK-Calls ersetzt.

        // Ãƒâ€žNDERUNG 3: _isInitialized als Guard gegen Mehrfach-
        // Initialisierung. Ein SemaphoreSlim(1,1) stellt sicher,
        // dass auch bei parallelen Aufrufen nur einer durchkommt.
        private bool _isInitialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private DateTime _lastBanCheckAt = DateTime.MinValue;
        private bool _lastBanCheckPassed = true;
        private const int BanCheckCooldownMinutes = 5;

        public AuthService(
            Supabase.Client supabaseClient,
            ILogger<AuthService> logger)
        {
            // Ãƒâ€žNDERUNG 4: Kein 'supabaseApiKey'-Parameter mehr.
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
            // Ãƒâ€žNDERUNG 5: SemaphoreSlim verhindert parallele
            // Initialisierung. WaitAsync(0) gibt sofort false zurÃƒÂ¼ck
            // wenn bereits jemand wartet, ohne zu blockieren.
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return; // Double-checked nach dem Lock

                await _supabaseClient.InitializeAsync();
                _isInitialized = true;
                _logger.LogInformation("Supabase-Client erfolgreich initialisiert.");

                // Ãƒâ€žNDERUNG 6: LoadSessionFromSecureStorageAsync entfernt.
                // Der SupabaseSessionHandler in MauiProgram.cs lÃƒÂ¤dt die
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

        // Ãƒâ€žNDERUNG 7: IsAuthenticatedAsync Ã¢â€ â€™ IsAuthenticated (synchron).
        // Die Methode liest nur eine Property Ã¢â‚¬â€œ kein await, kein Netzwerk.
        // Ein 'Async'-Suffix zu vergeben, nur weil Task.FromResult()
        // benutzt wird, ist irrefÃƒÂ¼hrend. Aufrufer wÃƒÂ¼rden einen await
        // schreiben, der technisch nie wartet.
        public bool IsAuthenticated =>
            _supabaseClient.Auth.CurrentSession != null;

        // Ãƒâ€žNDERUNG 8: GetCurrentUserIdAsync Ã¢â€ â€™ GetCurrentUserId (synchron)
        // Selbe BegrÃƒÂ¼ndung: kein Netzwerkzugriff, kein await nÃƒÂ¶tig.
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
                    _logger.LogWarning("Login fehlgeschlagen Ã¢â‚¬â€œ keine User-Daten zurÃƒÂ¼ckgegeben.");
                    return false;
                }

                // Ãƒâ€žNDERUNG 9: Kein manuelles SecureStorage.SetAsync mehr.
                // SupabaseSessionHandler speichert die Session automatisch
                // nachdem SignIn() intern die Session setzt.
                _logger.LogInformation(
                    "Login erfolgreich fÃƒÂ¼r {Email}.", result.User.Email);
                return true;
            }
            catch (Exception ex)
            {
                // Ãƒâ€žNDERUNG 10: Structured Logging statt String-Interpolation.
                // _logger.LogError(ex, $"Fehler: {ex.Message}") ist doppelt Ã¢â‚¬â€œ
                // die Exception enthÃƒÂ¤lt bereits die Message. Und Interpolation
                // verhindert, dass Log-Systeme (AppCenter, Serilog) die
                // Parameter separat indizieren kÃƒÂ¶nnen.
                _logger.LogError(ex, "Fehler beim Login fÃƒÂ¼r {Email}.", email);
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
                // Wir nutzen SignIn, um die GÃ¼ltigkeit des Passworts zu prÃ¼fen.
                // Supabase behÃ¤lt die Session bei oder aktualisiert sie.
                var result = await _supabaseClient.Auth.SignIn(email, password);
                return result?.User != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Passwort-Validierung fehlgeschlagen fÃ¼r {Email}.", email);
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
                        "Registrierung fehlgeschlagen fÃƒÂ¼r {Email} Ã¢â‚¬â€œ kein User zurÃƒÂ¼ckgegeben.", email);
                    return false;
                }

                if (!Guid.TryParse(result.User.Id, out var userId))
                {
                    _logger.LogWarning(
                        "Registrierung fehlgeschlagen fuer {Email}: Ungueltige User-ID aus Auth-Rueckgabe ({UserId}).",
                        email,
                        result.User.Id);
                    return false;
                }
                _logger.LogInformation(
                    "Auth-Registrierung erfolgreich: {Email}, Id={UserId}.", email, userId);

                // Ãƒâ€žNDERUNG 11: Kein raw HttpClient mehr. Das Supabase SDK
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
                    "DB-Eintrag erfolgreich erstellt fÃƒÂ¼r {Username}.", username);

                // Ãƒâ€žNDERUNG 12: Kein SecureStorage.SetAsync Ã¢â‚¬â€œ SupabaseSessionHandler
                // ÃƒÂ¼bernimmt das nach dem SignUp automatisch.
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Registrierung fÃƒÂ¼r {Email}.", email);
                return false;
            }
        }

        // Ãƒâ€žNDERUNG 13: IsUsernameValid Ã¢â€ â€™ IsUsernameAvailableAsync
        // (besserer Name Ã¢â‚¬â€œ "valid" ist mehrdeutig, "available" ist klar)
        // Kein raw HttpClient mehr, SDK-Abfrage stattdessen.
        public async Task<bool> IsUsernameAvailableAsync(string username)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_username", username }
                };

                var response = await _supabaseClient
                    .Rpc("is_username_available_safe", rpcParams)
                    .ConfigureAwait(false);

                bool available = !string.IsNullOrWhiteSpace(response?.Content)
                    && System.Text.Json.JsonSerializer.Deserialize<bool>(response.Content);

                _logger.LogInformation(
                    "Username '{Username}' verfÃƒÂ¼gbar: {Available}.", username, available);

                return available;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei IsUsernameAvailableAsync fÃƒÂ¼r '{Username}'.", username);
                return false; // Im Zweifel: nicht verfÃƒÂ¼gbar (sicherer)
            }
        }

        // --------------------------------------------------------
        // Session-Verwaltung
        // --------------------------------------------------------

        // Ãƒâ€žNDERUNG 14: ValidateAndRefreshSessionAsync entfernt.
        // Es war ein Duplikat von EnsureSessionValidAsync mit
        // leicht anderem Verhalten. Eine Methode fÃƒÂ¼r eine Aufgabe.
        public async Task<bool> EnsureSessionValidAsync()
        {
            try
            {
                var session = _supabaseClient.Auth.CurrentSession;

                if (session == null)
                {
                    // Ãƒâ€žNDERUNG 15: Kein manuelles SecureStorage lesen.
                    // Der SupabaseSessionHandler hat die Session bereits
                    // beim App-Start in den Client geladen. Wenn hier
                    // null zurÃƒÂ¼ckkommt, gibt es schlicht keine Session.
                    _logger.LogInformation("Keine aktive Session vorhanden.");
                    return false;
                }

                if (!session.Expired())
                {
                    return await ValidateBanStatusAsync();
                }

                _logger.LogInformation("Session abgelaufen - versuche Refresh.");
                var refreshed = await _supabaseClient.Auth.RefreshSession();

                if (refreshed != null && !refreshed.Expired())
                {
                    // SupabaseSessionHandler speichert automatisch
                    _logger.LogInformation("Session erfolgreich aktualisiert.");
                    return await ValidateBanStatusAsync();
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

        private async Task<bool> ValidateBanStatusAsync()
        {
            if ((DateTime.UtcNow - _lastBanCheckAt).TotalMinutes < BanCheckCooldownMinutes)
            {
                return _lastBanCheckPassed;
            }

            try
            {
                var currentUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId) || !Guid.TryParse(currentUserId, out var uid))
                {
                    _lastBanCheckAt = DateTime.UtcNow;
                    _lastBanCheckPassed = true;
                    return true;
                }

                var userResp = await _supabaseClient
                    .From<NBH.Resources.Models.User>()
                    .Where(u => u.Id == uid)
                    .Get();

                var user = userResp.Models?.FirstOrDefault();
                if (user != null && user.IsBanned)
                {
                    _lastBanCheckAt = DateTime.UtcNow;
                    _lastBanCheckPassed = false;
                    _logger.LogWarning("Nutzer {UserId} ist gesperrt. Abmeldung.", currentUserId);
                    await _supabaseClient.Auth.SignOut();
                    return false;
                }

                _lastBanCheckAt = DateTime.UtcNow;
                _lastBanCheckPassed = true;
                return true;
            }
            catch (Exception banEx)
            {
                _logger.LogWarning(banEx, "Ban-Check Fehler (nicht kritisch).");
                return true;
            }
        }
        public async Task<string> GetUserAccessTokenAsync()
        {
            bool valid = await EnsureSessionValidAsync();
            if (!valid)
            {
                _logger.LogWarning("Kein gÃƒÂ¼ltiges AccessToken verfÃƒÂ¼gbar.");
                return null;
            }
            return _supabaseClient.Auth.CurrentSession?.AccessToken;
        }

        public async Task SetSessionAsync(string accessToken, string refreshToken)
        {
            try
            {
                await _supabaseClient.Auth.SetSession(accessToken, refreshToken, false);
                // Ãƒâ€žNDERUNG 16: Kein manuelles SecureStorage.SetAsync.
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
                // Ãƒâ€žNDERUNG 17: Kein manuelles SecureStorage.Remove.
                // SupabaseSessionHandler ruft DestroySession() auf,
                // wenn SignOut() die Session im Client lÃƒÂ¶scht.
                // Nur Legacy-Keys aufrÃƒÂ¤umen, die der Handler nicht kennt:
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

        // Ãƒâ€žNDERUNG 18: Kein raw HttpClient mehr. Supabase SDK.
        public async Task<bool> UpdateUserProfileAsync(string userId, string profileImageUrl)
        {
            try
            {
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    _logger.LogWarning("UpdateUserProfileAsync: Ungueltige userId '{UserId}'.", userId);
                    return false;
                }
                await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Set(u => u.ProfileImageUrl, profileImageUrl)
                    .Update();

                _logger.LogInformation(
                    "Profilbild aktualisiert fÃƒÂ¼r UserId {UserId}.", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Fehler beim Aktualisieren des Profilbilds fÃƒÂ¼r UserId {UserId}.", userId);
                return false;
            }
        }

        // Ãƒâ€žNDERUNG 19: Neue Methode Ã¢â‚¬â€œ aus dem LoginPageViewModel hierher
        // verschoben. Datenbankoperationen gehÃƒÂ¶ren in Services, nicht
        // in ViewModels. Das war der AuslÃƒÂ¶ser fÃƒÂ¼r diese Refaktorierung.
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
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    _logger.LogWarning(
                        "UpdateOneSignalSubscription: Ungueltige userId '{UserId}'.", userId);
                    return;
                }

                // Nur aktualisieren, wenn sich die ID geÃƒÂ¤ndert hat
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
                    "OneSignal SubscriptionId aktualisiert fÃƒÂ¼r UserId {UserId}.", userId);
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
        // Ãƒâ€žNDERUNG 20: GetCurrentSessionAsync und GetApiKey entfernt.
        //
        // GetCurrentSessionAsync: Hat EnsureSessionValidAsync aufgerufen
        // und dann die Session zurÃƒÂ¼ckgegeben. Aufrufer kÃƒÂ¶nnen das selbst
        // tun (erst EnsureSessionValidAsync, dann GetUserAccessTokenAsync).
        // Eine Methode, die zwei andere aufruft nur um das Ergebnis
        // durchzureichen, ist unnÃƒÂ¶tige KomplexitÃƒÂ¤t.
        //
        // GetApiKey: Gibt den rohen API-Key nach auÃƒÅ¸en. Andere Klassen
        // sollten den Key nicht kennen. PhoenixRealtimeService bekommt
        // seinen Key direkt beim Erstellen in MauiProgram.cs.
        // --------------------------------------------------------
    }
}
