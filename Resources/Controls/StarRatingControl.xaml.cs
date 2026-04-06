using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;
using System.Diagnostics;
using System;

namespace NBH.Resources.Controls
{
    public partial class StarRatingControl : ContentView
    {
        public static readonly BindableProperty RatingProperty =
            BindableProperty.Create(
                nameof(Rating),
                typeof(double),
                typeof(StarRatingControl),
                0.0,
                BindingMode.TwoWay,
                propertyChanged: OnRatingChanged);

        public static readonly BindableProperty RatingCountProperty =
            BindableProperty.Create(
                nameof(RatingCount),
                typeof(int),
                typeof(StarRatingControl),
                0,
                BindingMode.TwoWay,
                propertyChanged: OnRatingCountChanged);

        public static readonly BindableProperty IsInteractiveProperty =
            BindableProperty.Create(
                nameof(IsInteractive),
                typeof(bool),
                typeof(StarRatingControl),
                true,
                propertyChanged: OnIsInteractiveChanged);

        public double Rating
        {
            get => (double)GetValue(RatingProperty);
            set => SetValue(RatingProperty, value);
        }

        public int RatingCount
        {
            get => (int)GetValue(RatingCountProperty);
            set => SetValue(RatingCountProperty, value);
        }

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public ICommand SetRatingCommand { get; }

        public StarRatingControl()
        {
            InitializeComponent();

            SetRatingCommand = new Command<object>(param =>
            {
                if (!IsInteractive) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    double rating = 0;
                    if (param is string s && double.TryParse(s, out double parsed))
                        rating = parsed;
                    else if (param is int i)
                        rating = i;
                    else if (param is double d)
                        rating = d;

                    Debug.WriteLine($"[DEBUG] SetRatingCommand triggered with rating: {rating}");
                    Rating = rating;
                    UpdateStars(rating);
                });
            });
            UpdateInteractiveState();
        }

        private static void OnRatingChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is StarRatingControl control)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    double newRating = (double)newValue;
                    control.UpdateStars(newRating);
                });
            }
        }

        private static void OnRatingCountChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is StarRatingControl control)
            {
                control.UpdateVisibility((int)newValue);
            }
        }

        private static void OnIsInteractiveChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is StarRatingControl control)
            {
                control.UpdateInteractiveState();
            }
        }

        private void UpdateInteractiveState()
        {
            bool interactive = IsInteractive;
            var starsLayout = this.FindByName<HorizontalStackLayout>("StarsLayout");
            if (starsLayout == null) return;

            foreach (var child in starsLayout.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Button btn)
                        {
                            btn.IsVisible = interactive;
                        }
                    }
                }
            }
        }

        private void UpdateVisibility(int count)
        {
            var starsLayout = this.FindByName<HorizontalStackLayout>("StarsLayout");
            var noRatingsLabel = this.FindByName<Label>("NoRatingsLabel");

            if (starsLayout == null || noRatingsLabel == null) return;

            if (count <= 0 && !IsInteractive)
            {
                starsLayout.IsVisible = false;
                noRatingsLabel.IsVisible = true;
            }
            else
            {
                starsLayout.IsVisible = true;
                noRatingsLabel.IsVisible = false;
            }
        }

        private void UpdateStars(double rating)
        {
            var starsLayout = this.FindByName<HorizontalStackLayout>("StarsLayout");
            if (starsLayout == null) return;

            int counter = 1;
            foreach (var child in starsLayout.Children)
            {
                if (child is Grid grid)
                {
                    var fillImage = grid.FindByName<Image>($"Star{counter}Fill");
                    if (fillImage != null)
                    {
                        double fillVal = Math.Clamp(rating - (counter - 1), 0.0, 1.0);
                        if (fillVal > 0)
                        {
                            fillImage.Opacity = 1;
                            if (fillVal < 1.0)
                            {
                                if (fillImage.Width > 0)
                                {
                                    ClipStarFill(fillImage, fillVal);
                                }
                                else
                                {
                                    double localVal = fillVal;
                                    EventHandler handler = null;
                                    handler = (s, ev) =>
                                    {
                                        fillImage.SizeChanged -= handler;
                                        ClipStarFill(fillImage, localVal);
                                    };
                                    fillImage.SizeChanged += handler;
                                }
                            }
                            else
                            {
                                fillImage.Clip = null;
                            }
                        }
                        else
                        {
                            fillImage.Opacity = 0;
                            fillImage.Clip = null;
                        }
                    }
                    counter++;
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
    }
}
