using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using NBH.Services;
using NBH.Resources.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using NBH.Resources.Models;
using System.Diagnostics;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CropImagePage : ContentPage
    {
        private Stream _uiStream;
        private double _scale = 1;
        private double _translationX = 0;
        private double _translationY = 0;
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;

        private static readonly string SupabaseUrl = AppConfig.SupabaseUrl;
        private static readonly string SupabaseKey = AppConfig.SupabaseAnonKey;
        private const string BucketName = "profile_pictures";

        public CropImagePage()
        {
            InitializeComponent();
            Debug.WriteLine("CropImagePage: Konstruktor aufgerufen.");

            _authService = ((App)Application.Current).GetServiceProvider().GetRequiredService<AuthService>();
            _httpClient = new HttpClient();

            OverlayGraphicsView.Drawable = new CropOverlayDrawable();
            Debug.WriteLine("CropImagePage: Overlay gesetzt.");

            _uiStream = NavigationService.CropImageStream;
            if (_uiStream != null)
            {
                Debug.WriteLine("CropImagePage: _uiStream != null => Zeige im ManipulatedImage.");
                var uiCopy = new MemoryStream();
                _uiStream.Position = 0;
                _uiStream.CopyTo(uiCopy);
                uiCopy.Position = 0;

                ManipulatedImage.Source = ImageSource.FromStream(() =>
                {
                    uiCopy.Position = 0;
                    return uiCopy;
                });
            }
            else
            {
                Debug.WriteLine("CropImagePage: _uiStream == null => Abbruch?");
            }
        }

        private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (e.Status == GestureStatus.Running)
            {
                _scale *= e.Scale;
                ManipulatedImage.Scale = _scale;
                Debug.WriteLine($"OnPinchUpdated => _scale={_scale}");
            }
        }

        private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            if (e.StatusType == GestureStatus.Running)
            {
                double deltaX = e.TotalX * 0.3;
                double deltaY = e.TotalY * 0.3;

                _translationX += deltaX;
                _translationY += deltaY;

                double maxX = (ManipulatedImage.Width * _scale) / 3;
                double maxY = (ManipulatedImage.Height * _scale) / 3;

                _translationX = Math.Max(-maxX, Math.Min(maxX, _translationX));
                _translationY = Math.Max(-maxY, Math.Min(maxY, _translationY));

                ManipulatedImage.TranslationX = _translationX;
                ManipulatedImage.TranslationY = _translationY;

                Debug.WriteLine($"OnPanUpdated => transX={_translationX}, transY={_translationY}");
            }
        }

        private async void OnCropButtonClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            await button.ScaleTo(0.9, 100);
            await button.ScaleTo(1.0, 100);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            Debug.WriteLine("OnCropButtonClicked => Start final Crop (200x200) mit Pan/Pinch");

            byte[] uiBytes;
            {
                var memCopy = new MemoryStream();
                _uiStream.Position = 0;
                _uiStream.CopyTo(memCopy);
                uiBytes = memCopy.ToArray();
            }

            double displayedImageWidth = ManipulatedImage.Width * ManipulatedImage.Scale;
            double displayedImageHeight = ManipulatedImage.Height * ManipulatedImage.Scale;
            double overlayWidth = OverlayGraphicsView.Width;
            double overlayHeight = OverlayGraphicsView.Height;

            var finalStream = await Task.Run(() =>
            {
                return DoFinalCropWithPanPinch(
                    uiBytes,
                    displayedImageWidth,
                    displayedImageHeight,
                    overlayWidth,
                    overlayHeight
                );
            });

            if (finalStream == null)
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync("Zuschneiden fehlgeschlagen. Bitte versuche es erneut.");
                return;
            }

            var uploadedUrl = await UploadCroppedImageAsync(finalStream);
            if (string.IsNullOrEmpty(uploadedUrl))
            {
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);
                await ToastService.ShowErrorAsync(UserFriendlyErrors.Messages.ImageUploadError);
                return;
            }

        }

        private MemoryStream DoFinalCropWithPanPinch(
            byte[] uiBytes,
            double displayedW,
            double displayedH,
            double overlayW,
            double overlayH)
        {
            try
            {
                if (uiBytes == null || uiBytes.Length == 0)
                {
                    Debug.WriteLine("DoFinalCropWithPanPinch => uiBytes=null/leer");
                    return null;
                }

                using var ms = new MemoryStream(uiBytes);
                using var img = SixLabors.ImageSharp.Image.Load(ms);

                int originalWidth = img.Width;
                int originalHeight = img.Height;

                double scaleFactorX = (double)originalWidth / displayedW;
                double scaleFactorY = (double)originalHeight / displayedH;

                double deviceCircleDiameter = 200.0;
                double radius = deviceCircleDiameter / 2.0;

                double centerX = overlayW / 2.0;
                double centerY = overlayH / 2.0;

                double leftDevice = centerX - radius;
                double topDevice = centerY - radius;

                double leftImage = (leftDevice - _translationX);
                double topImage = (topDevice - _translationY);
                double sizeImage = deviceCircleDiameter;

                leftImage /= _scale;
                topImage /= _scale;
                sizeImage /= _scale;

                int ix = (int)(leftImage * scaleFactorX);
                int iy = (int)(topImage * scaleFactorY);
                int iW = (int)(sizeImage * scaleFactorX);
                int iH = (int)(sizeImage * scaleFactorY);

                ix = Math.Max(0, Math.Min(originalWidth - iW, ix));
                iy = Math.Max(0, Math.Min(originalHeight - iH, iy));
                iW = Math.Min(iW, originalWidth - ix);
                iH = Math.Min(iH, originalHeight - iy);

                if (iW <= 0 || iH <= 0)
                {
                    Debug.WriteLine("DoFinalCropWithPanPinch => Ausschnitt zu klein oder fehlerhaft.");
                    return null;
                }

                Debug.WriteLine($"CropRect=({ix},{iy},{iW},{iH}) baseImg=({originalWidth}x{originalHeight})");

                img.Mutate(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(ix, iy, iW, iH)));

                var outStream = new MemoryStream();
                img.SaveAsPng(outStream);
                outStream.Position = 0;
                return outStream;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DoFinalCropWithPanPinch => Fehler: " + ex.Message);
                return null;
            }
        }

        private async Task<string> UploadCroppedImageAsync(Stream finalCropped)
        {
            try
            {
                // 1) User-JWT (fÃ¼r Auth) holen
                var userJwt = await SecureStorage.GetAsync("jwt_token");
                if (string.IsNullOrEmpty(userJwt))
                {
                    userJwt = await _authService.GetUserAccessTokenAsync();
                    await SecureStorage.SetAsync("jwt_token", userJwt);
                }
                if (string.IsNullOrEmpty(userJwt))
                {
                    Debug.WriteLine("UploadCroppedImageAsync => userJwt ist leer => RLS likely fails");
                }

                // 2) Eindeutigen Dateinamen erzeugen
                var fileName = $"{Guid.NewGuid()}_cropped.png";

                // Bucket je nach Modus auswÃ¤hlen
                string targetBucket = NavigationService.ImageCropMode == "Event" ? "event_pictures" : "profile_pictures";

                // 3) Ziel-URL fÃ¼r den Upload
                var url = $"{SupabaseUrl}/storage/v1/object/{targetBucket}/{fileName}";

                // 4) Multipart-Inhalt mit PNG anlegen
                using var formContent = new MultipartFormDataContent();
                var sc = new StreamContent(finalCropped);
                sc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                formContent.Add(sc, "file", fileName);

                // 5) HTTP-Header vorbereiten
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", userJwt);
                _httpClient.DefaultRequestHeaders.Add("apikey", SupabaseKey);

                // 6) POST an Supabase Storage
                var response = await _httpClient.PostAsync(url, formContent);
                Debug.WriteLine($"UploadCroppedImageAsync => {response.StatusCode}");

                // 7) Wenn erfolgreich hochgeladen
                if (response.IsSuccessStatusCode)
                {
                    // Ã–ffentlicher Pfad fÃ¼r das hochgeladene Bild
                    var pubUrl = $"{SupabaseUrl}/storage/v1/object/public/{targetBucket}/{fileName}";
                    Debug.WriteLine("UploadCroppedImageAsync => success => " + pubUrl);

                    if (NavigationService.ImageCropMode == "Event")
                    {
                        // Statt DB-Update schicken wir die URL per Message ans ViewModel
                        WeakReferenceMessenger.Default.Send(new EventImageUploadedMessage(pubUrl));
                        
                        // Setze sicherheitshalber wieder auf Profile zurÃ¼ck
                        NavigationService.ImageCropMode = "Profile";

                        await Shell.Current.GoToAsync("..");
                        return pubUrl;
                    }
                    else
                    {
                        // 8) DB patchen (profile_image_url)
                        var patchSuccess = await UpdateUserProfileImageAsync(fileName);
                        if (!patchSuccess)
                        {
#if IOS
                            iOSHapticFeedback.TriggerHeavyImpact();
#endif
                            HapticFeedback.Perform(HapticFeedbackType.Click);
                            await ToastService.ShowErrorAsync("Profilbild konnte nicht gespeichert werden. Bitte versuche es erneut.");
                            return null;
                        }

                        // 9) Daten neu laden (damit das aktualisierte Bild sofort in ViewModels ankommt)
                        try
                        {
                            Debug.WriteLine("Profilbild aktualisiert => Lade Profil neu...");

                            // Aus NavigationStack die ProfilePage holen und VM aktualisieren
                            var navStack = Shell.Current.Navigation.NavigationStack;
                            var profilePageInstance = navStack?.FirstOrDefault(pg => pg is ProfilePage) as ProfilePage;
                            if (profilePageInstance != null && profilePageInstance.BindingContext is ProfilePageViewModel profileVM)
                            {
                                // Weil es ein IAsyncRelayCommand ist, kÃ¶nnen wir ExecuteAsync aufrufen
                                await profileVM.LoadOwnProfileCommand.ExecuteAsync(null);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Fehler beim Neubinden des Profilbilds: " + ex.Message);
                        }

#if IOS
                        iOSHapticFeedback.TriggerHeavyImpact();
#endif
                        HapticFeedback.Perform(HapticFeedbackType.Click);
                        // 10) Benutzerfeedback
                        await ToastService.ShowSuccessAsync("Profilbild erfolgreich aktualisiert!");

                        // 11) Navigation zurÃ¼ck zur Profilseite
                        await Shell.Current.GoToAsync("//ProfilePage");

                        return pubUrl;
                    }
                }
                else
                {
                    // Fehler beim Hochladen
                    Debug.WriteLine($"UploadCroppedImageAsync => Fehler: {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UploadCroppedImageAsync => Exception: " + ex.Message);
                return null;
            }
        }



        private async Task<bool> UpdateUserProfileImageAsync(string filename)
        {
            try
            {
                var userId = _authService.GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.WriteLine("UpdateUserProfileImageAsync => userId leer => return false");
                    return false;
                }

                var updateData = new { profile_image_url = filename };
                var bodyJson = JsonConvert.SerializeObject(updateData);

                var query = new Dictionary<string, string>
                {
                { "id", userId }
                };
                var encodedQuery = new FormUrlEncodedContent(query);
                var url = $"{SupabaseUrl}/rest/v1/users?{await encodedQuery.ReadAsStringAsync()}";

                var userJwt = await _authService.GetUserAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", userJwt);
                _httpClient.DefaultRequestHeaders.Add("apikey", SupabaseKey);

                var jsonContent = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PatchAsync(url, jsonContent);
                Debug.WriteLine($"UpdateUserProfileImageAsync => {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UpdateUserProfileImageAsync => Exception: " + ex.Message);
                return false;
            }
        }
    }
}

