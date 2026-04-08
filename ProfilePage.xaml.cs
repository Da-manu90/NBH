using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NBH.Resources.ViewModels;
using Microsoft.Maui.Devices;
using NBH.Resources.Models;
using NBH.Resources.Views;
using System.Threading;
using System.Threading.Tasks;
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

            // Scale-Animation fuer visuelles Feedback
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
            await frame.ScaleTo(1.0, 100);  // Zurueck zur Normalgroesse
        }

        private async void OnVerifyQualificationsTapped(object sender, EventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            var frame = (Frame)sender;
            await frame.ScaleTo(0.9, 100);  // Verkleinern
            await frame.ScaleTo(1.0, 100);  // Zurueck zur Normalgroesse
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

        private async void OnSettingsButtonClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton button)
            {
                return;
            }

            try
            {
#if IOS
                iOSHapticFeedback.TriggerLightImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);

                // Spin with gradual slowdown: fast start, then decelerate
                var navigationTask = Shell.Current.GoToAsync(Routes.SettingsPage);
                await SpinWithDecelerationAsync(button, navigationTask);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] Settings Navigation Fehler: {ex.Message}");
            }
        }

        private static async Task SpinWithDecelerationAsync(VisualElement element, Task navigationTask)
        {
            // Phase 1: Fast spin (at least 2 full rotations while navigating)
            uint speed = 120;
            for (int i = 0; i < 6; i++)
            {
                await element.RotateTo(element.Rotation + 180, speed, Easing.Linear);
            }

            // Ensure navigation is done before winding down
            await navigationTask;

            // Phase 2: Decelerate — each half-rotation gets progressively slower
            uint[] deceleratingSpeeds = [180, 260, 380, 550];
            foreach (var ms in deceleratingSpeeds)
            {
                await element.RotateTo(element.Rotation + 180, ms, Easing.CubicOut);
            }

            // Snap to nearest full rotation (0°)
            var remainder = element.Rotation % 360;
            if (remainder > 0)
            {
                await element.RotateTo(element.Rotation + (360 - remainder), 300, Easing.CubicOut);
            }
            element.Rotation = 0;
        }
    }
}




