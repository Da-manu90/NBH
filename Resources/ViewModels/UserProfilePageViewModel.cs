using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using NBH.Resources.Models;
using NBH.Services;
using Newtonsoft.Json;
using Supabase;
using static Supabase.Postgrest.Constants;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public partial class UserProfilePageViewModel : ObservableObject, IQueryAttributable
    {
        // ----------------------------------------------------------------
        // Felder
        // ----------------------------------------------------------------
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<UserProfilePageViewModel> _logger;
        private readonly AuthService _authService;
        private readonly ModerationService _moderationService;

        // Speichere die userId, die wir anzeigen
        private string userId;

        private const string BucketName = "profile_pictures";

        // ----------------------------------------------------------------
        // Konstruktor
        // ----------------------------------------------------------------
        public UserProfilePageViewModel(
            AuthService authService,
            ILogger<UserProfilePageViewModel> logger,
            Supabase.Client supabaseClient,
            ModerationService moderationService
        )
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _moderationService = moderationService ?? throw new ArgumentNullException(nameof(moderationService));

            LoadUserProfileCommand = new AsyncRelayCommand<string>(LoadUserProfileAsync);
            NavigateToRatingPageCommand = new Command(async () => await NavigateToRatingPageAsync());
            GoBackCommand = new Command(async () => await GoBackAsync());
            StartChatCommand = new Command(async () => await CreateNewChatAsync());
            ShowOptionsCommand = new Command(async () => await ShowOptionsAsync());
        }

        // ----------------------------------------------------------------
        // Observable Properties
        // ----------------------------------------------------------------
        [ObservableProperty]
        private bool _isVerified;

        [ObservableProperty]
        private string wohnort;

        [ObservableProperty]
        private string ueberMich;

        partial void OnUeberMichChanged(string value)
        {
            if (value?.Length > 400)
            {
                ueberMich = value.Substring(0, 400);
                OnPropertyChanged(nameof(UeberMich));
            }
        }


        [ObservableProperty]
        private string qualifikationen;

        [ObservableProperty]
        private int entriesCreated;

        [ObservableProperty]
        private int entriesParticipated;

        private string _profileImageUrl = "profile_placeholder.png";
        public string ProfileImageUrl
        {
            get => _profileImageUrl;
            set => SetProperty(ref _profileImageUrl, value);
        }

        private string _username = "";
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private double _finalRating;
        public double FinalRating
        {
            get => _finalRating;
            set => SetProperty(ref _finalRating, value);
        }

        private int _finalRatingCount;
        public int FinalRatingCount
        {
            get => _finalRatingCount;
            set => SetProperty(ref _finalRatingCount, value);
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------
        public IAsyncRelayCommand<string> LoadUserProfileCommand { get; }
        public ICommand NavigateToRatingPageCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand StartChatCommand { get; }
        public ICommand ShowOptionsCommand { get; }

        public Action<double, int>? UpdateStarsAction { get; set; }

        // ----------------------------------------------------------------
        // IQueryAttributable
        // ----------------------------------------------------------------
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("userId", out var userIdObj) && userIdObj is string receivedUserId)
            {
                _logger.LogInformation($"Empfangene Benutzer-ID: {receivedUserId}");
                userId = receivedUserId;
                LoadUserProfileCommand.Execute(userId);
            }
        }

        // ----------------------------------------------------------------
        // Profil des fremden Users laden
        // ----------------------------------------------------------------
        private async Task LoadUserProfileAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("LoadUserProfileAsync: Benutzer-ID ist leer.");
                return;
            }

            // Prüfen, ob userId in Guid konvertierbar ist
            if (!Guid.TryParse(userId, out var userGuid))
            {
                _logger.LogWarning("LoadUserProfileAsync: userId kann nicht in GUID konvertiert werden.");
                return;
            }

            try
            {
                // 1) User-Datensatz aus Supabase laden
                if (await TryLoadProfileSummaryAsync(userGuid))
                {
                    _logger.LogInformation("Benutzerprofil erfolgreich per RPC-Zusammenfassung geladen.");
                    return;
                }

                var response = await _supabaseClient.Rpc("get_public_user_profile", new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                });

                var foundUser = string.IsNullOrWhiteSpace(response?.Content)
                    ? null
                    : JsonConvert.DeserializeObject<PublicUserProfile>(response.Content);
                if (foundUser == null)
                {
                    _logger.LogInformation("LoadUserProfileAsync: Kein User-Datensatz gefunden.");
                    return;
                }

                ApplyUserProfile(
                    foundUser.Username,
                    foundUser.ProfileImageUrl,
                    foundUser.Wohnort,
                    foundUser.UeberMich,
                    foundUser.Qualifikationen,
                    foundUser.IsVerified);

                EntriesCreated = 0;
                EntriesParticipated = 0;
                _logger.LogInformation("Benutzerprofil per Public-Profile-Fallback geladen => Starte Rating-Berechnung");
                await CalculateDetailedRatingAsync(userGuid);
                return;

                #if false
                // 2) Anzeige-Felder setzen
                ProfileImageUrl = string.IsNullOrWhiteSpace(foundUser.ProfileImageUrl)
                    ? "profile_placeholder.png"
                    : _supabaseClient.Storage
                                     .From(BucketName)
                                     .GetPublicUrl(foundUser.ProfileImageUrl);

                Username = foundUser.Username;
                Wohnort = foundUser.Wohnort;
                UeberMich = foundUser.UeberMich;
                Qualifikationen = foundUser.Qualifikationen;
                IsVerified = foundUser.IsVerified;

                // 3) Rating für diesen User berechnen
                _logger.LogInformation("Benutzerprofil erfolgreich geladen => Starte Rating-Berechnung");
                await CalculateDetailedRatingAsync(userGuid);
                #endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Benutzerprofils (LoadUserProfileAsync).");
            }
        }

        private async Task<bool> TryLoadProfileSummaryAsync(Guid userGuid)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                };

                var response = await _supabaseClient.Rpc("get_user_profile_summary", rpcParams);
                if (string.IsNullOrWhiteSpace(response?.Content))
                {
                    return false;
                }

                var summary = JsonConvert.DeserializeObject<UserProfileSummary>(response.Content);
                if (summary?.User == null)
                {
                    return false;
                }

                ApplyUserProfile(
                    summary.User.Username,
                    summary.User.ProfileImageUrl,
                    summary.User.Wohnort,
                    summary.User.UeberMich,
                    summary.User.Qualifikationen,
                    summary.User.IsVerified);

                FinalRating = Math.Round(summary.Rating?.FinalRating ?? 0.0, 2);
                FinalRatingCount = summary.Rating?.TotalCount ?? 0;
                EntriesCreated = summary.EntriesCreated;
                EntriesParticipated = summary.EntriesParticipated;
                UpdateStarsAction?.Invoke(FinalRating, FinalRatingCount);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "get_user_profile_summary nicht verfuegbar oder fehlgeschlagen, nutze Fallback.");
                return false;
            }
        }

        private void ApplyUserProfile(
            string? username,
            string? profileImagePath,
            string? wohnort,
            string? ueberMich,
            string? qualifikationen,
            bool isVerified)
        {
            ProfileImageUrl = string.IsNullOrWhiteSpace(profileImagePath)
                ? "profile_placeholder.png"
                : _supabaseClient.Storage
                    .From(BucketName)
                    .GetPublicUrl(profileImagePath);

            Username = username ?? string.Empty;
            Wohnort = wohnort ?? string.Empty;
            UeberMich = ueberMich ?? string.Empty;
            Qualifikationen = qualifikationen ?? string.Empty;
            IsVerified = isVerified;
        }

        // ----------------------------------------------------------------
        // Rating via bestehender Aggregat-RPC laden
        // ----------------------------------------------------------------
        private async Task CalculateDetailedRatingAsync(Guid userGuid)
        {
            try
            {
                var rpcParams = new Dictionary<string, object>
                {
                    { "p_user_id", userGuid }
                };

                var response = await _supabaseClient.Rpc("get_user_rating_stats", rpcParams);
                if (string.IsNullOrWhiteSpace(response?.Content))
                {
                    FinalRating = 0.0;
                    FinalRatingCount = 0;
                    return;
                }

                var stats = JsonConvert.DeserializeObject<RatingStats>(response.Content);
                FinalRating = Math.Round(stats?.FinalRating ?? 0.0, 2);
                FinalRatingCount = stats?.TotalCount ?? 0;

                _logger.LogInformation($"Rating => {FinalRating:F2} ({FinalRatingCount} Bewertungen)");
                UpdateStarsAction?.Invoke(FinalRating, FinalRatingCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Berechnen der Bewertung");
            }
        }

        // ----------------------------------------------------------------
        // Navigation: Zur RatingPage
        // ----------------------------------------------------------------
        private async Task NavigateToRatingPageAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // "RatingPage?ReviewedUserId={userId}"
                    var encodedUserId = Uri.EscapeDataString(userId);
                    await Shell.Current.GoToAsync($"{nameof(RatingPage)}?ReviewedUserId={encodedUserId}");
                }
                else
                {
                    _logger.LogWarning("userId war leer => keine Navigation möglich.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei Navigation zur RatingPage.");
            }
        }

        // ----------------------------------------------------------------
        // Back Button
        // ----------------------------------------------------------------
        private async Task GoBackAsync()
        {
            try
            {
                _logger.LogInformation("Zurück zur vorherigen Seite navigieren...");

                // Mit Shell: "eine Seite zurück" = GoToAsync("..")
                // Sofern DetailPage im Stack liegt
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Navigieren zurück (GoBackAsync).");
            }
        }

        // ----------------------------------------------------------------
        // Chat erstellen oder vorhandenen öffnen
        // ----------------------------------------------------------------
        private async Task CreateNewChatAsync()
        {
            _logger.LogInformation("CreateNewChatAsync gestartet.");

            if (string.IsNullOrEmpty(userId))
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ChatStartError);
                _logger.LogWarning("CreateNewChatAsync: fremde Benutzer-ID ist leer.");
                return;
            }

            try
            {
                var currentUserIdStr = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserIdStr))
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ChatStartError);
                    _logger.LogWarning("CreateNewChatAsync: Aktuelle Benutzer-ID ist null oder leer.");
                    return;
                }

                if (!Guid.TryParse(currentUserIdStr, out var currentUserGuid) ||
                    !Guid.TryParse(userId, out var otherUserGuid))
                {
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ChatStartError);
                    _logger.LogWarning("CreateNewChatAsync: Benutzer-IDs können nicht in GUID umgewandelt werden.");
                    return;
                }

                // 2) Prüfe, ob (user_id == currentUserId && partner_id == otherUserId)
                var createChatRpcParams = new Dictionary<string, object>
                {
                    { "p_partner_id", otherUserGuid }
                };

                var createChatResponse = await _supabaseClient.Rpc("find_or_create_chat_safe", createChatRpcParams);
                var existingOrNewChatId = string.IsNullOrWhiteSpace(createChatResponse?.Content)
                    ? Guid.Empty
                    : JsonConvert.DeserializeObject<Guid>(createChatResponse.Content);

                if (existingOrNewChatId != Guid.Empty)
                {
                    _logger.LogInformation("Chat {ChatId} erfolgreich gefunden oder erstellt.", existingOrNewChatId);
                    await Shell.Current.GoToAsync($"ChatPage?chatId={existingOrNewChatId}");
                    return;
                }

                _logger.LogWarning("Konnte Chat nicht erstellen oder finden.");
                return;

#if false
                var q1 = await _supabaseClient
                    .From<PublicChat>()
                    .Filter("user_id", Operator.Equals, currentUserIdStr)
                    .Filter("partner_id", Operator.Equals, userId)
                    .Get();

                // 3) Prüfe, ob (user_id == otherUserId && partner_id == currentUserId)
                var q2 = await _supabaseClient
                    .From<PublicChat>()
                    .Filter("user_id", Operator.Equals, userId)
                    .Filter("partner_id", Operator.Equals, currentUserIdStr)
                    .Get();

                // 4) Zusammenführen
                var existingChats = q1.Models
                    .Concat(q2.Models)
                    .Distinct()
                    .ToList();

                // 5) Wenn bereits ein Chat existiert => zum ersten Chat navigieren
                var found = existingChats.FirstOrDefault();
                if (found != null)
                {
                    _logger.LogInformation($"Existierender Chat gefunden: {found.Id}");
                    await Shell.Current.GoToAsync($"ChatPage?chatId={found.Id}");
                    return;
                }

                // 6) Neuen Chat anlegen
                var newChat = new PublicChat
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserIdStr,
                    PartnerId = userId,
                    LastMessage = string.Empty,
                    LastMessageDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                var insertResp = await _supabaseClient
                    .From<PublicChat>()
                    .Insert(newChat);

                if (insertResp.ResponseMessage?.IsSuccessStatusCode == true)
                {
                    _logger.LogInformation($"Neuer Chat {newChat.Id} erfolgreich erstellt.");
                    await Shell.Current.GoToAsync($"ChatPage?chatId={newChat.Id}");
                }
                else
                {
                    _logger.LogWarning($"Konnte Chat nicht erstellen => {insertResp.ResponseMessage?.StatusCode}");
                }
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen des Chats.");
            }
        }

        // ----------------------------------------------------------------
        // Options-Menü (Nutzer melden/blockieren)
        // ----------------------------------------------------------------
        private async Task ShowOptionsAsync()
        {
            var action = await Shell.Current.DisplayActionSheet(
                "Optionen", "Abbrechen", null,
                "Nutzer melden", "Nutzer blockieren");

            if (action == "Nutzer melden" && !string.IsNullOrEmpty(userId))
            {
                var reason = await Shell.Current.DisplayActionSheet(
                    "Grund wählen", "Abbrechen", null,
                    "Spam", "Unangemessener Inhalt", "Falsche Informationen", "Belästigung", "Anderes");

                if (reason != null && reason != "Abbrechen")
                {
                    if (!Guid.TryParse(userId, out var reportedUserId))
                    {
                        await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ReportError);
                        return;
                    }

                    var success = await _moderationService.ReportUserAsync(reportedUserId, reason, null);
                    if (success)
                        await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.ReportSent);
                    else
                        await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ReportError);
                }
            }
            else if (action == "Nutzer blockieren" && !string.IsNullOrEmpty(userId))
            {
                if (!Guid.TryParse(userId, out var blockedUserId))
                {
                    await ToastService.ShowErrorAsync("Nutzer-ID ungültig.");
                    return;
                }

                _moderationService.BlockUser(blockedUserId);
                await ToastService.ShowInfoAsync("Nutzer wurde blockiert.");
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}

