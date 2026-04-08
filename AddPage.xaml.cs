using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using NBH.Resources.ViewModels;
using Microsoft.Extensions.Logging;
using Supabase;
using NBH.Services;
using System;
using System.Threading.Tasks;
using NBH.Resources.Models;
using Microsoft.Maui.Devices;
using System.Diagnostics;
using System.Windows.Input;
using NBH.Resources.Views;
#if IOS
using NBH.Platforms.iOS;
#endif
using NBH.Resources.Animations;

namespace NBH
{
    public partial class AddPage : ContentPage
    {
        public NBH.Resources.Controls.BellControl BellControl => MyBell;

        private AddPageViewModel _viewModel;

        public AddPage()
        {
            InitializeComponent();

            // ViewModel aus dem Service-Container
            _viewModel = ((App)Application.Current!).GetServiceProvider().GetRequiredService<AddPageViewModel>();
            BindingContext = _viewModel;
        }

        private async void OnCreateEventTapped(object sender, EventArgs e)
        {
            if (sender is VisualElement el)
            {
                await AnimationHelper.ButtonPressAsync(el);
            }
            await Shell.Current.GoToAsync(Routes.CreateEvent);
        }

        // ----------------------------------------------------------------
        // CollectionView-Item getappt => KURZ ZOOM + HAPTIC + COMMAND
        // ----------------------------------------------------------------
        private async void OnEntryTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement itemElement)
            {
                // (A) Kurz grÃƒÂ¶ÃƒÅ¸er werden
                await itemElement.ScaleTo(1.2, 100, Easing.CubicOut);
                await itemElement.ScaleTo(1.0, 100, Easing.CubicIn);

                // (B) Haptic
#if IOS
                iOSHapticFeedback.TriggerHeavyImpact();
#endif
                HapticFeedback.Perform(HapticFeedbackType.Click);

                // (C) Welches EntryItem ist getappt?
                var tappedItem = e.Parameter as EntryItem;
                if (tappedItem != null)
                {
                    // (D) OpenDetailCommand aus dem VM aufrufen
                    _viewModel.OpenDetailCommand?.Execute(tappedItem);
                }
            }
        }

        // ----------------------------------------------------------------
        // NEU: Pagination-Buttons getappt => BOUNCE + HAPTIC + Command
        // ----------------------------------------------------------------
        private async void OnNavFrameTapped(object sender, TappedEventArgs e)
        {
            // (1) Bounce-Animation
            if (sender is VisualElement navElement)
            {
                await AnimateBounce(navElement);
            }

            // (2) Haptic Feedback
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // (3) Das Command aufrufen (im e.Parameter)
            if (e.Parameter is ICommand cmd)
            {
                cmd.Execute(null);
            }
        }

        // ----------------------------------------------------------------
        // Bounce-Animation (wie in DetailPage)
        // ----------------------------------------------------------------
        private async Task AnimateBounce(VisualElement element)
        {
            // Kleiner werden
            await element.ScaleTo(0.8, 100, Easing.CubicIn);
            // Etwas grÃƒÂ¶ÃƒÅ¸er als normal
            await element.ScaleTo(1.1, 100, Easing.CubicOut);
            // ZurÃƒÂ¼ck auf OriginalgrÃƒÂ¶ÃƒÅ¸e
            await element.ScaleTo(1.0, 50, Easing.Linear);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // TabBar visuell auf diesen Tab setzen (ohne erneute Navigation)
                MyTabBar.SetActiveTabWithoutNavigation(2);  // 2 = Neu/Add

                await MyBell.UpdateBadgeAsync();

                // Entrance Animations
                await AnimationHelper.PageEntranceAsync(CreateEventBtn);

                if (_viewModel.Entries.Count == 0)
                {
                    await _viewModel.LoadEntriesFromDatabaseAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddPage] OnAppearing Fehler: {ex.Message}");
            }
        }
    }
}

