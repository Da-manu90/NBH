using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace NBH.Resources.Controls
{
    public partial class LoadingOverlay : ContentView
    {
        public static readonly BindableProperty LoadingTextProperty =
            BindableProperty.Create(nameof(LoadingText), typeof(string), typeof(LoadingOverlay), "Bitte warten...", propertyChanged: OnLoadingTextChanged);

        public string LoadingText
        {
            get { return (string)GetValue(LoadingTextProperty); }
            set { SetValue(LoadingTextProperty, value); }
        }

        private static void OnLoadingTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is LoadingOverlay overlay)
            {
                overlay.LoadingLabel.Text = (string)newValue;
            }
        }

        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public async Task ShowAsync(string text = null)
        {
            if (text != null)
                LoadingText = text;
            
            IsVisible = true;
            Opacity = 0;
            await this.FadeTo(1, 200, Easing.CubicOut);
        }

        public async Task HideAsync()
        {
            await this.FadeTo(0, 200, Easing.CubicIn);
            IsVisible = false;
        }
    }
}
