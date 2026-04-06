using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using Microsoft.Maui.Controls.Shapes;

namespace NBH.Resources.Controls;

public partial class AvatarStackControl : ContentView
{
    public static readonly BindableProperty ImageUrlsProperty =
        BindableProperty.Create(nameof(ImageUrls), typeof(IEnumerable<string>), typeof(AvatarStackControl), null, propertyChanged: OnImageUrlsChanged);

    public IEnumerable<string> ImageUrls
    {
        get => (IEnumerable<string>)GetValue(ImageUrlsProperty);
        set => SetValue(ImageUrlsProperty, value);
    }

    public static readonly BindableProperty AvatarSizeProperty =
        BindableProperty.Create(nameof(AvatarSize), typeof(double), typeof(AvatarStackControl), 32.0, propertyChanged: OnImageUrlsChanged);

    public double AvatarSize
    {
        get => (double)GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public static readonly BindableProperty MaxVisibleProperty =
        BindableProperty.Create(nameof(MaxVisible), typeof(int), typeof(AvatarStackControl), 5, propertyChanged: OnImageUrlsChanged);

    public int MaxVisible
    {
        get => (int)GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    public AvatarStackControl()
    {
        InitializeComponent();
    }

    private static void OnImageUrlsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AvatarStackControl control)
        {
            control.BuildStack();
        }
    }

    private void BuildStack()
    {
        RootLayout.Children.Clear();
        if (ImageUrls == null) return;

        int count = 0;
        int totalImages = 0;
        foreach (var url in ImageUrls) totalImages++;

        foreach (var url in ImageUrls)
        {
            if (count >= MaxVisible) break;

            var border = new Border
            {
                WidthRequest = AvatarSize,
                HeightRequest = AvatarSize,
                Stroke = Colors.White,
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(AvatarSize / 2) }
            };

            var image = new Image
            {
                Source = string.IsNullOrEmpty(url) ? "profile_placeholder.png" : url,
                Aspect = Aspect.AspectFill,
                Clip = new EllipseGeometry { Center = new Point(AvatarSize / 2, AvatarSize / 2), RadiusX = AvatarSize / 2, RadiusY = AvatarSize / 2 }
            };

            border.Content = image;
            RootLayout.Children.Add(border);
            count++;
        }

        if (totalImages > MaxVisible)
        {
            var overflowCount = totalImages - MaxVisible;
            var border = new Border
            {
                WidthRequest = AvatarSize,
                HeightRequest = AvatarSize,
                Stroke = Colors.White,
                StrokeThickness = 2,
                BackgroundColor = Colors.LightGray,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(AvatarSize / 2) }
            };

            var label = new Label
            {
                Text = $"+{overflowCount}",
                TextColor = Colors.Black,
                FontSize = AvatarSize * 0.4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontAttributes = FontAttributes.Bold
            };

            border.Content = label;
            RootLayout.Children.Add(border);
        }
    }
}
