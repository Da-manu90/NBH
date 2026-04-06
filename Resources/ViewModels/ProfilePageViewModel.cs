using System;
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
    public partial class ProfilePageViewModel : ObservableObject
    {
        // ----------------------------------------------------------------
        // Felder
        // ----------------------------------------------------------------
        private readonly Supabase.Client _supabaseClient;
        private readonly AuthService _authService;
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
        public IAsyncRelayCommand OpenPrivacyPolicyCommand { get; }

        // ----------------------------------------------------------------
        // Konstruktor
        // ----------------------------------------------------------------
        public ProfilePageViewModel(
            Supabase.Client supabaseClient,
            AuthService authService,
            ILogger<ProfilePageViewModel> logger)
        {
            _supabaseClient = supabaseClient;
            _authService = authService;
            _logger = logger;

            // Commands
            LoadOwnProfileCommand = new AsyncRelayCommand(LoadOwnProfileAsync);
            SaveProfileAsyncCommand = new AsyncRelayCommand(SaveProfileAsync);
            SelectAndUploadImageCommand = new AsyncRelayCommand(SelectAndCropImageAsync);
            LogoutCommand = new AsyncRelayCommand(LogoutAsync);
            SendFileByEmailCommand = new AsyncRelayCommand(SendFileByEmailAsync);
            ResetPasswordCommand = new Command(async () => await ExecuteResetPasswordAsync());
            NavigateToSettingsCommand = new Command(async () => await Shell.Current.GoToAsync("SettingsPage"));
            NavigateToRatingPageCommand = new RelayCommand(async () =>
            {
                var userId = _authService.GetCurrentUserId();
                if (!string.IsNullOrEmpty(userId))
                    await Shell.Current.GoToAsync($"RatingPage?ReviewedUserId={userId}");
            });
            OpenPrivacyPolicyCommand = new AsyncRelayCommand(OpenPrivacyPolicyAsync);
        }

        // ----------------------------------------------------------------
        // Privacy Policy öffnen
        // ----------------------------------------------------------------
        private async Task OpenPrivacyPolicyAsync()
        {
            await Browser.OpenAsync("https://nbh-app.de/privacy", BrowserLaunchMode.SystemPreferred);
        }

        // ----------------------------------------------------------------
        // Passwort zurücksetzen
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

                // userId als GUID interpretieren
                if (!Guid.TryParse(userId, out var userGuid))
                {
                    _logger.LogWarning("UserID kann nicht in GUID geparst werden.");
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

                ProfileImageUrl = string.IsNullOrWhiteSpace(foundUser.ProfileImageUrl)
                    ? "profile_placeholder.png"
                    : _supabaseClient.Storage
                        .From(BucketName)
                        .GetPublicUrl(foundUser.ProfileImageUrl);

                Username = foundUser.Username ?? string.Empty;
                Wohnort = foundUser.Wohnort ?? string.Empty;
                UeberMich = foundUser.UeberMich ?? string.Empty;
                Qualifikationen = foundUser.Qualifikationen ?? string.Empty;

                await CalculateDetailedRatingAsync(userGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei LoadOwnProfileAsync");
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

                // Event-Bewertungen laden
                var erResp = await _supabaseClient.From<EventRating>()
                    .Filter("reviewed_id", Supabase.Postgrest.Constants.Operator.Equals, userIdStr)
                    .Get();

                // Chat-Bewertungen laden
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Berechnen der Bewertung");
            }
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
                    await Shell.Current.GoToAsync("//LoginPage");
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
        // Bild auswählen & skalieren
        // ----------------------------------------------------------------
        private async Task SelectAndCropImageAsync()
        {
            try
            {
                var file = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = FilePickerFileType.Images,
                    PickerTitle = "Wähle ein Bild aus"
                });

                if (file == null)
                {
                    Debug.WriteLine("Kein Bild ausgewählt - Vorgang abgebrochen.");
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

                Application.Current.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
                await Shell.Current.GoToAsync("//LoginPage");
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
