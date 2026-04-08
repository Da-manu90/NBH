// ============================================================
// LoginPage.xaml.cs - Polished Version
//
// Angepasst an das aktuelle Projekt:
// - Parameterloser Konstruktor (fuer Shell/ContentTemplate)
// - DI-Konstruktor (fuer direkte Injection)
// - iOS Haptic Feedback
// - PageEntranceAsync Animation
// - Fokus-Animationen auf Entry-Borders
// - Fehler-Banner statt hartes Label
// - Passwort-Sichtbarkeit Toggle
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using NBH.Resources.Animations;
using NBH.Resources.ViewModels;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using System.Diagnostics;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    public partial class LoginPage : ContentPage
    {
        // --------------------------------------------------------
        // Parameterloser Konstruktor - fuer Shell / ContentTemplate
        // --------------------------------------------------------
        public LoginPage()
            : this(((App)Application.Current!).GetServiceProvider().GetRequiredService<LoginPageViewModel>())
        {
            Debug.WriteLine("[LoginPage] Parameterloser Konstruktor aufgerufen.");
        }

        // --------------------------------------------------------
        // DI-Konstruktor
        // --------------------------------------------------------
        public LoginPage(LoginPageViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            Debug.WriteLine("[LoginPage] DI-Konstruktor aufgerufen. BindingContext gesetzt auf " + viewModel.GetType().Name);

            // Passwortfeld: Enter druecken -> Fokus entfernen
            PasswordEntry.Completed += (s, e) => PasswordEntry.Unfocus();

            // Fehler-Binding beobachten um Banner zu animieren
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // --------------------------------------------------------
        // EINGANGSANIMATION: Drei Sektionen gestaffelt einblenden
        // --------------------------------------------------------
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[LoginPage] OnAppearing aufgerufen. BindingContext = " + (BindingContext?.GetType().Name ?? "null"));

            // iOS Safe Area entfernen
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                this.Padding = new Thickness(0);
            }

            // Kurze Pause damit das Layout fertig gemessen ist
            await Task.Delay(50);

            // Gestaffelte Eingangsanimation: Logo -> Formular -> Buttons
            await AnimationHelper.PageEntranceAsync(
                LogoSection,
                FormSection,
                ButtonSection
            );
        }

        // --------------------------------------------------------
        // FOKUS-ANIMATIONEN: Border leuchtet beim Tippen auf
        // --------------------------------------------------------
        private async void OnEmailFocused(object sender, FocusEventArgs e)
        {
            EmailBorder.Stroke = Color.FromArgb("#00897b");
            await EmailBorder.ScaleTo(1.01, 150, Easing.CubicOut);
        }

        private async void OnEmailUnfocused(object sender, FocusEventArgs e)
        {
            EmailBorder.Stroke = Color.FromArgb("#DDDDDD");
            await EmailBorder.ScaleTo(1.0, 150, Easing.CubicIn);
        }

        private async void OnPasswordFocused(object sender, FocusEventArgs e)
        {
            PasswordBorder.Stroke = Color.FromArgb("#00897b");
            await PasswordBorder.ScaleTo(1.01, 150, Easing.CubicOut);
        }

        private async void OnPasswordUnfocused(object sender, FocusEventArgs e)
        {
            PasswordBorder.Stroke = Color.FromArgb("#DDDDDD");
            await PasswordBorder.ScaleTo(1.0, 150, Easing.CubicIn);
        }

        // --------------------------------------------------------
        // BUTTON MICRO-INTERACTIONS
        // --------------------------------------------------------
        private async void OnLoginTapped(object sender, TappedEventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            await AnimationHelper.ButtonPressAsync(LoginButton);
        }

        private async void OnRegisterTapped(object sender, TappedEventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            await AnimationHelper.ButtonPressAsync(RegisterButton);
        }

        private async void OnAppleTapped(object sender, TappedEventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            await AnimationHelper.ButtonPressAsync(AppleButton);
        }

        private async void OnGoogleTapped(object sender, TappedEventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerMediumImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            await AnimationHelper.ButtonPressAsync(GoogleButton);
        }

        private async void OnFacebookTapped(object sender, TappedEventArgs e)
        {
#if IOS
            iOSHapticFeedback.TriggerMediumImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
            await AnimationHelper.ButtonPressAsync(FacebookButton);
        }

        // --------------------------------------------------------
        // FEHLER-BANNER: Sanft einblenden statt hartes Label
        // --------------------------------------------------------
        private async void OnViewModelPropertyChanged(
            object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LoginPageViewModel.ErrorMessage)
             && e.PropertyName != nameof(LoginPageViewModel.IsErrorVisible))
                return;

            var vm = (LoginPageViewModel)BindingContext;

            if (vm.IsErrorVisible && !string.IsNullOrEmpty(vm.ErrorMessage))
            {
                // Formular schuetteln + Banner einblenden
                await Task.WhenAll(
                    AnimationHelper.ShakeAsync(FormSection),
                    ShowErrorBanner()
                );
            }
            else
            {
                await HideErrorBanner();
            }
        }

        private async Task ShowErrorBanner()
        {
            ErrorBanner.IsVisible = true;
            await ErrorBanner.FadeTo(1, 250, Easing.CubicOut);
        }

        private async Task HideErrorBanner()
        {
            await ErrorBanner.FadeTo(0, 200, Easing.CubicIn);
            ErrorBanner.IsVisible = false;
        }

        // --------------------------------------------------------
        // PASSWORT ANZEIGEN/VERBERGEN
        // --------------------------------------------------------
        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
            EyeButton.Source = PasswordEntry.IsPassword
                ? "eye.png"
                : "eyeoff.png";
        }
    }
}
