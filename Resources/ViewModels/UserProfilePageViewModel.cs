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
                var resp = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Get();

                var foundUser = resp.Models.FirstOrDefault();
                if (foundUser == null)
                {
                    _logger.LogInformation("LoadUserProfileAsync: Kein User-Datensatz gefunden.");
                    return;
                }

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden des Benutzerprofils (LoadUserProfileAsync).");
            }
        }

        // ----------------------------------------------------------------
        // Rating direkt aus event_rating + chat_reviews berechnen
        // ----------------------------------------------------------------
        private async Task CalculateDetailedRatingAsync(Guid userGuid)
        {
            try
            {
                var userIdStr = userGuid.ToString();

                var erResp = await _supabaseClient.From<EventRating>()
                    .Filter("reviewed_id", Supabase.Postgrest.Constants.Operator.Equals, userIdStr)
                    .Get();

                var crResp = await _supabaseClient.From<ChatReview>()
                    .Filter("reviewed_id", Supabase.Postgrest.Constants.Operator.Equals, userIdStr)
                    .Get();

                var erScores = (erResp.Models ?? new List<EventRating>())
                    .Select(r => (r.event_friendliness + r.description_accuracy + r.reliability) / 3.0);

                var crScores = (crResp.Models ?? new List<ChatReview>())
                    .Select(r => (r.friendliness + r.response_time + r.helpfulness) / 3.0);

                var allScores = erScores.Concat(crScores).ToList();

                FinalRating = allScores.Count > 0 ? Math.Round(allScores.Average(), 2) : 0.0;
                FinalRatingCount = allScores.Count;

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
                    var success = await _moderationService.ReportUserAsync(Guid.Parse(userId), reason, null);
                    if (success)
                        await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.ReportSent);
                    else
                        await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ReportError);
                }
            }
            else if (action == "Nutzer blockieren" && !string.IsNullOrEmpty(userId))
            {
                _moderationService.BlockUser(Guid.Parse(userId));
                await ToastService.ShowInfoAsync("Nutzer wurde blockiert.");
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}

