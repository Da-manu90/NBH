using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NBH.Resources.ViewModels;
using Microsoft.Maui.Devices;
using NBH.Resources.Models;
using NBH.Resources.Views;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfilePageViewModel _viewModel;

        public ProfilePage(ProfilePageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;

            // Profil laden
            // (AsyncRelayCommand => wir rufen es einmalig auf)
            _viewModel.LoadOwnProfileCommand.Execute(null);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // TabBar visuell auf diesen Tab setzen (ohne erneute Navigation)
                MyTabBar.SetActiveTabWithoutNavigation(0);  // 0 = Profil

                await MyBell.UpdateBadgeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] OnAppearing Fehler: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private async void Logout_Tapped(object sender, TappedEventArgs e)
        {
            var frame = (Frame)sender;

            // Haptic Feedback (Vibration)
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Scale-Animation fÃƒÂ¼r visuelles Feedback
            await frame.ScaleTo(0.9, 100);
            await frame.ScaleTo(1.0, 100);
        }

        private async void OnSaveProfileTapped(object sender, EventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            var frame = (Frame)sender;
            await frame.ScaleTo(0.9, 100);  // Verkleinern
            await frame.ScaleTo(1.0, 100);  // ZurÃƒÂ¼ck zur Normalgröße
        }

        private async void OnVerifyQualificationsTapped(object sender, EventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            var frame = (Frame)sender;
            await frame.ScaleTo(0.9, 100);  // Verkleinern
            await frame.ScaleTo(1.0, 100);  // ZurÃƒÂ¼ck zur Normalgröße
        }



        private async void OnProfileImageTapped(object sender, EventArgs e)
        {
            if (ProfileImage == null) return;

            // Scale up
            await ProfileImage.ScaleTo(1.25, 120, Easing.CubicOut);
            // Scale down
            await ProfileImage.ScaleTo(1.0, 80, Easing.CubicIn);

#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Command zum Bild-Upload aufrufen
            _viewModel.SelectAndUploadImageCommand?.Execute(null);
        }
    }
}




