using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NBH.Services;
using Newtonsoft.Json;
using NBH.Resources.Models;
using Supabase;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
// Alias, um Kollisionen mit Microsoft.Maui.Controls.Image zu umgehen:
using SImage = SixLabors.ImageSharp.Image;
using SResizeOptions = SixLabors.ImageSharp.Processing.ResizeOptions;
using SResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using SSize = SixLabors.ImageSharp.Size;
using System.Windows.Input;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH.Resources.ViewModels
{
    public class ProfileActivityCard
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#00897b";
    }

    public partial class ProfilePageViewModel : ObservableObject
    {
        // ----------------------------------------------------------------
        // Felder
        // ----------------------------------------------------------------
        private readonly Supabase.Client _supabaseClient;
        private readonly AuthService _authService;
        private readonly AppSessionFlowService _appSessionFlowService;
        private readonly AppWarmupService _appWarmupService;
        private readonly ILogger<ProfilePageViewModel> _logger;

        private const string BucketName = "profile_pictures";

        // ----------------------------------------------------------------
        // Observable Properties
        // ----------------------------------------------------------------
        [ObservableProperty]
        private bool isUploading;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private double uploadProgress;

        [ObservableProperty]
        private string profileImageUrl = "profile_placeholder.png";

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        private string wohnort;

        [ObservableProperty]
        private string ueberMich;

        [ObservableProperty]
        private string qualifikationen;

        [ObservableProperty]
        private int entriesCreated;

        [ObservableProperty]
        private int entriesParticipated;

        public ObservableCollection<ProfileActivityCard> ActivityCards { get; } = new();
        public ObservableCollection<ProfileBadge> BadgeItems { get; } = new();
        public bool HasActivityCards => ActivityCards.Count > 0;
        public bool HasRealActivity => EntriesCreated > 0 || EntriesParticipated > 0;
        public bool HasNoActivity => !HasRealActivity;
        public bool HasBadges => BadgeItems.Count > 0;

        [ObservableProperty]
        private bool isVerifiedUser;

        // ----------------------------------------------------------------
        // Rating-Properties
        // ----------------------------------------------------------------
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
        public IAsyncRelayCommand LoadOwnProfileCommand { get; }
        public IAsyncRelayCommand SaveProfileAsyncCommand { get; }
        public IAsyncRelayCommand SelectAndUploadImageCommand { get; }
        public IAsyncRelayCommand LogoutCommand { get; }
        public IAsyncRelayCommand SendFileByEmailCommand { get; }
        public IRelayCommand NavigateToRatingPageCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }
        public IRelayCommand<ProfileActivityCard> OpenActivityCardCommand { get; }
        public IAsyncRelayCommand OpenPrivacyPolicyCommand { get; }

        // ----------------------------------------------------------------
        // Konstruktor
        // ----------------------------------------------------------------
        public ProfilePageViewModel(
            Supabase.Client supabaseClient,
            AuthService authService,
            AppSessionFlowService appSessionFlowService,
            AppWarmupService appWarmupService,
            ILogger<ProfilePageViewModel> logger)
        {
            _supabaseClient = supabaseClient;
            _authService = authService;
            _appSessionFlowService = appSessionFlowService;
            _appWarmupService = appWarmupService;
            _logger = logger;

            // Commands
            LoadOwnProfileCommand = new AsyncRelayCommand(LoadOwnProfileAsync);
            SaveProfileAsyncCommand = new AsyncRelayCommand(SaveProfileAsync);
            SelectAndUploadImageCommand = new AsyncRelayCommand(SelectAndCropImageAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);
            SendFileByEmailCommand = new AsyncRelayCommand(SendFileByEmailAsync);
            ResetPasswordCommand = new Command(async () => await ExecuteResetPasswordAsync());
            NavigateToSettingsCommand = new Command(async () => await Shell.Current.GoToAsync(Routes.SettingsPage));
            OpenActivityCardCommand = new RelayCommand<ProfileActivityCard>(async card => await OpenActivityCardAsync(card));
            NavigateToRatingPageCommand = new RelayCommand(async () =>
            {
                var userId = _authService.GetCurrentUserId();
                if (!string.IsNullOrEmpty(userId))
                    await Shell.Current.GoToAsync($"RatingPage?ReviewedUserId={userId}");
            });
            OpenPrivacyPolicyCommand = new AsyncRelayCommand(OpenPrivacyPolicyAsync);
        }

        // ----------------------------------------------------------------
        // Privacy Policy oeffnen
        // ----------------------------------------------------------------
        private async Task OpenPrivacyPolicyAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/privacy", BrowserLaunchMode.SystemPreferred);
        }

        // ----------------------------------------------------------------
        // Passwort zuruecksetzen
        // ----------------------------------------------------------------
        private async Task ExecuteResetPasswordAsync()
        {
            // E-Mail des aktuell eingeloggten Nutzers aus Supabase holen
            var email = _authService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(email))
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync("Keine E-Mail-Adresse gefunden. Bitte melde dich erneut an.");
                return;
            }

            bool success = await _authService.SendPasswordResetEmailAsync(email);
            if (success)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.PasswordResetSent);
            }
            else
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.PasswordResetError);
            }
        }

        // ----------------------------------------------------------------
        // Profil laden
        // ----------------------------------------------------------------
        private async Task LoadOwnProfileAsync()
        {
            try
            {
                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Kein User ID vorhanden - Ladeprofil abgebrochen.");
                    return;
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    _logger.LogWarning("UserID kann nicht in GUID geparst werden.");
                    return;
                }

                if (await TryLoadProfileSummaryAsync(userGuid))
                {
                    _logger.LogInformation("Eigenes Profil erfolgreich per RPC-Zusammenfassung geladen.");
                    return;
                }

                var response = await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userGuid)
                    .Get();

                var foundUser = response.Models.FirstOrDefault();
                if (foundUser == null)
                {
                    _logger.LogInformation("Kein User-Datensatz gefunden.");
                    return;
                }

                ApplyUserProfile(
                    foundUser.Username,
                    foundUser.ProfileImageUrl,
                    foundUser.Wohnort,
                    foundUser.UeberMich,
                    foundUser.Qualifikationen);

                EntriesCreated = 0;
                EntriesParticipated = 0;
                await CalculateDetailedRatingAsync(userGuid);
                RebuildActivityCards();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei LoadOwnProfileAsync");
            }
        }

        private async Task<bool> TryLoadProfileSummaryAsync(Guid userGuid)
        {
            try
            {
                var cachedSummary = _appWarmupService.GetCachedOwnProfileSummary(userGuid);
                if (cachedSummary?.User != null)
                {
                    ApplyProfileSummary(cachedSummary);
                    return true;
                }

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

                ApplyProfileSummary(summary);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "get_user_profile_summary nicht verfuegbar oder fehlgeschlagen, nutze Fallback.");
                return false;
            }
        }

        private void ApplyProfileSummary(UserProfileSummary summary)
        {
            if (summary.User == null)
            {
                return;
            }

            ApplyUserProfile(
                summary.User.Username,
                summary.User.ProfileImageUrl,
                summary.User.Wohnort,
                summary.User.UeberMich,
                summary.User.Qualifikationen);

            FinalRating = Math.Round(summary.Rating?.FinalRating ?? 0.0, 2);
            FinalRatingCount = summary.Rating?.TotalCount ?? 0;
            EntriesCreated = summary.EntriesCreated;
            EntriesParticipated = summary.EntriesParticipated;
            IsVerifiedUser = summary.User.IsVerified;
            RebuildActivityCards();
            RebuildBadges();
        }

        private void ApplyUserProfile(
            string? username,
            string? profileImagePath,
            string? wohnort,
            string? ueberMich,
            string? qualifikationen)
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
            RebuildBadges();
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
                RebuildActivityCards();
                RebuildBadges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Berechnen der Bewertung");
            }
        }

        private void RebuildActivityCards()
        {
            ActivityCards.Clear();

            ActivityCards.Add(new ProfileActivityCard
            {
                Key = "created",
                Icon = "📌",
                Title = "Eigene Eintraege",
                Subtitle = $"{EntriesCreated} erstellt",
                AccentColor = "#00897b"
            });

            ActivityCards.Add(new ProfileActivityCard
            {
                Key = "participating",
                Icon = "🤝",
                Title = "Teilnahmen",
                Subtitle = $"{EntriesParticipated} bestaetigt",
                AccentColor = "#3f51b5"
            });

            ActivityCards.Add(new ProfileActivityCard
            {
                Key = "ratings",
                Icon = "⭐",
                Title = "Bewertung",
                Subtitle = FinalRatingCount > 0
                    ? $"{FinalRating:0.0} bei {FinalRatingCount} Bewertungen"
                    : "Noch keine Bewertungen",
                AccentColor = "#ef6c00"
            });

            OnPropertyChanged(nameof(HasActivityCards));
            OnPropertyChanged(nameof(HasRealActivity));
            OnPropertyChanged(nameof(HasNoActivity));
        }

        private async Task OpenActivityCardAsync(ProfileActivityCard? card)
        {
            if (card == null)
            {
                return;
            }

            if (card.Key == "created")
            {
                await Shell.Current.GoToAsync($"{Routes.MyEventsPage}?section=created");
                return;
            }

            if (card.Key == "participating")
            {
                await Shell.Current.GoToAsync($"{Routes.MyEventsPage}?section=participating");
                return;
            }

            if (card.Key == "ratings")
            {
                NavigateToRatingPageCommand.Execute(null);
            }
        }

        private void RebuildBadges()
        {
            BadgeItems.Clear();

            AddBadge("verified", "Verifiziert", "Account ist verifiziert.", "✅", "#00897B", IsVerifiedUser, IsVerifiedUser ? 1 : 0, null);
            AddBadge("starter", "Starter", "Erstes Event erstellt.", "🚀", "#00AEEF", EntriesCreated >= 1, Math.Min(1, EntriesCreated / 1.0), "add_icon.png");
            AddBadge("organizer", "Veranstalter-Profi", "Mind. 5 eigene Events.", "📅", "#2E7D32", EntriesCreated >= 5, Math.Min(1, EntriesCreated / 5.0), "add.png");
            AddBadge("teamplayer", "Teamplayer", "Mind. 3 Teilnahmen.", "🤝", "#3949AB", EntriesParticipated >= 3, Math.Min(1, EntriesParticipated / 3.0), "chat.png");
            AddBadge("toprated", "Top bewertet", "Ø 4.5+ bei mind. 5 Bewertungen.", "🌟", "#EF6C00", FinalRating >= 4.5 && FinalRatingCount >= 5, Math.Min(1, FinalRatingCount / 5.0), "star_filled32.png");
            AddBadge("qualified", "Qualifiziert", "Qualifikationen gepflegt.", "🛡️", "#6A1B9A", !string.IsNullOrWhiteSpace(Qualifikationen) && Qualifikationen.Trim().Length >= 20, Math.Min(1, (Qualifikationen?.Trim().Length ?? 0) / 20.0), "dp_benutzerinformationen.png");
            AddBadge("community", "Community Aktiv", "Insgesamt 8+ Aktivitaeten.", "🏅", "#00838F", EntriesCreated + EntriesParticipated >= 8, Math.Min(1, (EntriesCreated + EntriesParticipated) / 8.0), "profile.png");

            OnPropertyChanged(nameof(HasBadges));
        }

        private void AddBadge(string key, string title, string description, string icon, string color, bool unlocked, double progress, string? iconSource = null)
        {
            var clampedProgress = Math.Min(1.0, Math.Max(0.0, progress));

            BadgeItems.Add(new ProfileBadge
            {
                Key = key,
                Title = title,
                Description = description,
                IconText = icon,
                IconSource = iconSource,
                AccentColor = color,
                IsUnlocked = unlocked,
                Progress = clampedProgress
            });
        }

        // ----------------------------------------------------------------
        // Profil speichern
        // ----------------------------------------------------------------
        private async Task SaveProfileAsync()
        {
            IsBusy = true;
            try
            {
                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId)) return;
                if (!Guid.TryParse(userId, out var userGuid)) return;

                var session = _supabaseClient.Auth.CurrentSession;
                if (session == null || session.User == null)
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.SessionExpired);
                    await _appSessionFlowService.NavigateToLoginAsync();
                    return;
                }

                var updateResp = await _supabaseClient.From<User>()
                    .Where(u => u.Id == userGuid)
                    .Set(u => u.Wohnort, Wohnort)
                    .Set(u => u.UeberMich, UeberMich)
                    .Set(u => u.Qualifikationen, Qualifikationen)
                    .Update();

                if (updateResp != null && updateResp.Models.Any())
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowSuccessAsync(UserFriendlyErrors.Messages.ProfileSaved);
                }
                else
                {
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowInfoAsync(UserFriendlyErrors.Messages.NoChanges);
                }
            }
            catch (Exception ex)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.GetMessage(ex));
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ----------------------------------------------------------------
        // Bild auswaehlen & skalieren
        // ----------------------------------------------------------------
        private async Task SelectAndCropImageAsync()
        {
            try
            {
                var file = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = FilePickerFileType.Images,
                    PickerTitle = "Waehle ein Bild aus"
                });

                if (file == null)
                {
                    Debug.WriteLine("Kein Bild ausgewaehlt - Vorgang abgebrochen.");
                    return;
                }

                using var originalStream = await file.OpenReadAsync();
                using var image = await SImage.LoadAsync(originalStream);

                if (image == null)
                {
                    Debug.WriteLine("Fehler: Bild konnte nicht geladen werden.");
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.InvalidImage);
                    return;
                }

                image.Mutate(ctx =>
                {
                    ctx.Resize(new SResizeOptions
                    {
                        Mode = SResizeMode.Max,
                        Size = new SSize(600, 600)
                    });
                });

                using var outStream = new MemoryStream();
                var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 };
                await image.SaveAsync(outStream, encoder);
                outStream.Position = 0;

                if (outStream.Length == 0)
                {
                    Debug.WriteLine("Fehler: Skalierter Bild-Stream ist leer.");
#if IOS
                    iOSHapticFeedback.TriggerHeavyImpact();
#endif
                    HapticFeedback.Perform(HapticFeedbackType.Click);
                    await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ImageProcessingError);
                    return;
                }

                NavigationService.CropImageStream = outStream;
                await Shell.Current.GoToAsync(nameof(CropImagePage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Bildauswahlprozess: {ex.Message}");
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ImageUploadError);
            }
        }


        // ----------------------------------------------------------------
        // Logout
        // ----------------------------------------------------------------
        private async Task LogoutAsync()
        {
            try
            {
                SecureStorage.Remove("supabase_session");
                await _supabaseClient.Auth.SignOut();
                await _appSessionFlowService.NavigateToLoginAsync("Du wurdest abgemeldet.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Logout.");
            }
        }

        // ----------------------------------------------------------------
        // Datei per E-Mail senden
        // ----------------------------------------------------------------
        private async Task SendFileByEmailAsync()
        {
            try
            {
                var file = await FilePicker.PickAsync();
                if (file == null) return;

                var tempPath = Path.Combine(FileSystem.CacheDirectory, file.FileName);
                using (var input = await file.OpenReadAsync())
                using (var output = File.Create(tempPath))
                {
                    await input.CopyToAsync(output);
                }

                var email = new EmailMessage
                {
                    Subject = "Qualifikation",
                    Body = "Hier ist die Datei im Anhang."
                };
                email.Attachments.Add(new EmailAttachment(tempPath));

                await Email.Default.ComposeAsync(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim E-Mail-Versand.");
            }
        }
    }
}
