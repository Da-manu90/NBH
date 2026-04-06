using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace NBH.Resources.Controls;

public partial class CapacityBarControl : ContentView
{
    public static readonly BindableProperty FillPercentageProperty =
        BindableProperty.Create(nameof(FillPercentage), typeof(double), typeof(CapacityBarControl), 0.0, propertyChanged: OnFillPercentageChanged);

    public double FillPercentage
    {
        get => (double)GetValue(FillPercentageProperty);
        set => SetValue(FillPercentageProperty, value);
    }

    public static readonly BindableProperty CountTextProperty =
        BindableProperty.Create(nameof(CountText), typeof(string), typeof(CapacityBarControl), "0 von 0", propertyChanged: OnCountTextChanged);

    public string CountText
    {
        get => (string)GetValue(CountTextProperty);
        set => SetValue(CountTextProperty, value);
    }

    public static readonly BindableProperty ImageUrlsProperty =
        BindableProperty.Create(nameof(ImageUrls), typeof(IEnumerable<string>), typeof(CapacityBarControl), null, propertyChanged: OnImageUrlsChanged);

    public IEnumerable<string> ImageUrls
    {
        get => (IEnumerable<string>)GetValue(ImageUrlsProperty);
        set => SetValue(ImageUrlsProperty, value);
    }

    public CapacityBarControl()
    {
        InitializeComponent();
    }

    private static void OnFillPercentageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CapacityBarControl control && newValue is double percent)
        {
            control.UpdateFillBoxWidth(control.Width);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateFillBoxWidth(width);
    }

    private void UpdateFillBoxWidth(double containerWidth)
    {
        if (containerWidth > 0 && FillPercentage >= 0)
        {
            double safePercentage = FillPercentage > 100 ? 100 : FillPercentage;
            FillBox.WidthRequest = containerWidth * (safePercentage / 100.0);
            
            // Adjust colors based on percentage
            if (safePercentage >= 100)
            {
                FillBox.BackgroundColor = Color.FromArgb("#DC3545"); // Red for full
            }
            else if (safePercentage >= 80)
            {
                FillBox.BackgroundColor = Color.FromArgb("#FF8C00"); // Orange for almost full
            }
            else
            {
                FillBox.BackgroundColor = Color.FromArgb("#0066a5"); // Standard blue
            }
        }
    }

    private static void OnCountTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CapacityBarControl control && newValue is string text)
        {
            control.CountLabel.Text = text;
        }
    }

    private static void OnImageUrlsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CapacityBarControl control && newValue is IEnumerable<string> urls)
        {
            control.AvatarStack.ImageUrls = urls;
        }
    }
}
