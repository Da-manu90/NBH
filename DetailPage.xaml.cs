using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NBH.Resources.Models;
using NBH.Services;
using Microsoft.Maui.Devices;
using NBH.Resources.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using NBH.Resources.Views;
using CommunityToolkit.Mvvm.Messaging;
#if IOS
using NBH.Platforms.iOS;
#endif

namespace NBH
{
    [QueryProperty(nameof(SelectedEntry), "SelectedEntry")]
    [QueryProperty(nameof(EntryId), "EntryId")]
    public partial class DetailPage : ContentPage
    {

        private bool _isRegistered;
        private readonly DetailPageViewModel _viewModel;

        public EntryItem SelectedEntry
        {
            get => _viewModel.SelectedEntry;
            set => _viewModel.SelectedEntry = value;
        }

        public string EntryId { get; set; }

        public DetailPage(DetailPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = _viewModel = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_isRegistered)
            {
                WeakReferenceMessenger.Default.Register<PhoenixPostgresEvent>(this, (r, message) =>
                {
                    HandleEntryChanged(message);
                });
                _isRegistered = true;
            }

            await MyBell.UpdateBadgeAsync();
            _viewModel.UpdateStarsAction = UpdateStarsDetailed;
            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;

            if (_viewModel.SelectedEntry == null && !string.IsNullOrEmpty(EntryId))
            {
                await _viewModel.LoadEntryByIdAsync(Guid.Parse(EntryId));
            }

            UpdateStarsDetailed(_viewModel.FinalRating, _viewModel.FinalRatingCount);
        }


        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            WeakReferenceMessenger.Default.Unregister<PhoenixPostgresEvent>(this);
            _isRegistered = false;
        }


        private void ViewModelOnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.FinalRating) ||
                e.PropertyName == nameof(_viewModel.FinalRatingCount))
            {
                UpdateStarsDetailed(_viewModel.FinalRating, _viewModel.FinalRatingCount);
            }
        }

        private async void HandleEntryChanged(PhoenixPostgresEvent pgEvent)
        {
            if (pgEvent.New != null && pgEvent.New.TryGetValue("id", out var newIdObj))
            {
                if (Guid.TryParse(newIdObj.ToString(), out var changedId))
                {
                    // Prüfe, ob es sich um den Eintrag handelt, den wir anzeigen
                    if (_viewModel.SelectedEntry != null && changedId == _viewModel.SelectedEntry.id)
                    {
                        await _viewModel.RefreshEntryStateAsync();
                    }
                }
            }
        }

        private void UpdateStarsDetailed(double averageRating, int totalCount)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (totalCount == 0)
                {
                    RatingCountLabel.Text = "Keine Bewertungen";
                    StarStack.IsVisible = false;
                    return;
                }
                else
                {
                    RatingCountLabel.Text = $"{totalCount} Bewertungen";
                    StarStack.IsVisible = true;
                }
            });

            if (totalCount == 0)
                return;

            for (int starIndex = 1; starIndex <= 5; starIndex++)
            {
                double starValue = averageRating - (starIndex - 1);
                if (starValue < 0) starValue = 0;
                if (starValue > 1) starValue = 1;

                Image fillImage = starIndex switch
                {
                    1 => Star1Fill,
                    2 => Star2Fill,
                    3 => Star3Fill,
                    4 => Star4Fill,
                    5 => Star5Fill,
                    _ => null
                };

                if (fillImage == null) continue;

                if (fillImage.Width > 0)
                {
                    ClipStarFill(fillImage, starValue);
                }
                else
                {
                    EventHandler handler = null;
                    handler = (s, ev) =>
                    {
                        fillImage.SizeChanged -= handler;
                        ClipStarFill(fillImage, starValue);
                    };
                    fillImage.SizeChanged += handler;
                }
            }
        }

        private void ClipStarFill(Image fillImage, double starValue)
        {
            double w = fillImage.Width;
            double h = fillImage.Height;
            double clipWidth = w * starValue;

            fillImage.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, clipWidth, h)
            };
        }

        // ----------------------------------------------------------------
        // PROFILE IMAGE TAPPED => Rotate + Scale
        // ----------------------------------------------------------------
        private async void OnProfileImageTapped(object sender, TappedEventArgs e)
        {
            if (ProfileImage == null) return;

            await ProfileImage.ScaleTo(1.25, 100, Easing.CubicOut);
            await ProfileImage.ScaleTo(1.0, 50, Easing.Linear);

            // Haptic
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Danach VM-Command
            _viewModel.NavigateToUserProfileCommand?.Execute(null);
        }

        // ----------------------------------------------------------------
        // STAR STACK TAPPED => Fade + Scale
        // ----------------------------------------------------------------
        private async void OnStarsTapped(object sender, TappedEventArgs e)
        {
            if (StarStack == null) return;

            // Animation: Fade to 0.6 + Scale to 0.95
            await Task.WhenAll(
                StarStack.FadeTo(0.6, 100),
                StarStack.ScaleTo(0.95, 100)
            );
            // Dann zurück auf Normal
            await Task.WhenAll(
                StarStack.FadeTo(1.0, 200),
                StarStack.ScaleTo(1.0, 200)
            );

            // Haptic
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            // Dann VM-Command (NavigateToRatingPageCommand)
            _viewModel.NavigateToRatingPageCommand?.Execute(null);
        }

        // ----------------------------------------------------------------
        // BUTTONS => BOUNCE + HAPTIC
        // ----------------------------------------------------------------
        private async void SingleActionButton_Tapped(object sender, TappedEventArgs e)
        {
            await AnimateBounce(sender as VisualElement);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            _viewModel.EntryActionCommand?.Execute(null);
        }

        private async void DoubleActionButton_Tapped(object sender, TappedEventArgs e)
        {
            await AnimateBounce(sender as VisualElement);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            _viewModel.EntryActionCommand?.Execute(null);
        }

        private async void RejectButton_Tapped(object sender, TappedEventArgs e)
        {
            await AnimateBounce(sender as VisualElement);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            _viewModel.RejectEventCommand?.Execute(null);
        }

        private async void ContactButton_Tapped(object sender, TappedEventArgs e)
        {
            await AnimateBounce(sender as VisualElement);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            _viewModel.StartChatCommand?.Execute(null);
        }

        private async void BackButton_Tapped(object sender, TappedEventArgs e)
        {
            await AnimateBounce(sender as VisualElement);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);

            await Shell.Current.GoToAsync("..", true);
        }


        // ----------------------------------------------------------------
        // Gemeinsame Bounce-Animation
        // ----------------------------------------------------------------
        private async Task AnimateBounce(VisualElement element)
        {
            if (element == null) return;

            // Kleiner werden
            await element.ScaleTo(0.8, 100, Easing.CubicIn);
            // Etwas größer als normal
            await element.ScaleTo(1.1, 100, Easing.CubicOut);
            // Zurück auf Originalgröße
            await element.ScaleTo(1.0, 50, Easing.Linear);
#if IOS
            iOSHapticFeedback.TriggerHeavyImpact();
#endif
            HapticFeedback.Perform(HapticFeedbackType.Click);
        }
    }
}
