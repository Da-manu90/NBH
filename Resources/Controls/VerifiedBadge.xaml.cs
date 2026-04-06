using Microsoft.Maui.Controls;

namespace NBH.Resources.Controls
{
    public partial class VerifiedBadge : ContentView
    {
        public static readonly BindableProperty IsVerifiedProperty =
            BindableProperty.Create(nameof(IsVerified), typeof(bool), typeof(VerifiedBadge), false);

        public bool IsVerified
        {
            get => (bool)GetValue(IsVerifiedProperty);
            set => SetValue(IsVerifiedProperty, value);
        }

        public VerifiedBadge()
        {
            InitializeComponent();
        }
    }
}
