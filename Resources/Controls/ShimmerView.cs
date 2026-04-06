// ============================================================
// ShimmerView.cs – Skelett-Ladeanimation
//
// Ersetzt leere Seiten oder Spinner während Daten laden.
// Nutzer sehen sofort die Struktur der Seite, nicht ein
// weißes Nichts. Das ist der Standard bei WhatsApp,
// LinkedIn, Instagram und allen professionellen Apps.
//
// Verwendung in XAML:
//   <controls:ShimmerView IsShimmering="{Binding IsBusy}"
//                         WidthRequest="300"
//                         HeightRequest="80"
//                         CornerRadius="16" />
// ============================================================
using Microsoft.Maui.Controls.Shapes;


namespace NBH.Resources.Controls
{
    public class ShimmerView : ContentView
    {
        // BindableProperty damit IsBusy aus dem ViewModel bindbar ist
        public static readonly BindableProperty IsShimmeringProperty =
            BindableProperty.Create(
                nameof(IsShimmering),
                typeof(bool),
                typeof(ShimmerView),
                defaultValue: true,
                propertyChanged: OnIsShimmeringChanged);

        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(
                nameof(CornerRadius),
                typeof(double),
                typeof(ShimmerView),
                defaultValue: 12.0);

        public bool IsShimmering
        {
            get => (bool)GetValue(IsShimmeringProperty);
            set => SetValue(IsShimmeringProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        private readonly BoxView _baseView;
        private readonly BoxView _shimmerOverlay;
        private CancellationTokenSource _animationCts;

        public ShimmerView()
        {
            // Basis: dezentes Grau
            _baseView = new BoxView
            {
                Color = Color.FromArgb("#E8E8E8")
            };

            // Overlay: helleres Grau das über die Basis gleitet
            _shimmerOverlay = new BoxView
            {
                Color   = Color.FromArgb("#F5F5F5"),
                Opacity = 0
            };

            Content = new Grid
            {
                Children = { _baseView, _shimmerOverlay }
            };

            // Abgerundete Ecken über Clip
            UpdateClip();
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateClip();
        }

        private void UpdateClip()
        {
            if (Width > 0 && Height > 0)
            {
                Clip = new RoundRectangleGeometry(
                    new CornerRadius(CornerRadius),
                    new Rect(0, 0, Width, Height));
            }
        }

        private static void OnIsShimmeringChanged(
            BindableObject bindable,
            object oldValue,
            object newValue)
        {
            if (bindable is ShimmerView view)
            {
                if ((bool)newValue)
                    view.StartShimmer();
                else
                    view.StopShimmer();
            }
        }

        private void StartShimmer()
        {
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            IsVisible = true;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _shimmerOverlay.FadeTo(0.9, 600, Easing.SinInOut);
                        if (!token.IsCancellationRequested)
                            await _shimmerOverlay.FadeTo(0.0, 600, Easing.SinInOut);
                    });
                }
            }, token);
        }

        private void StopShimmer()
        {
            _animationCts?.Cancel();
            IsVisible = false;
        }
    }
}
