#if ANDROID
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace NBH.Platforms.Android
{
    public class CustomShellHandler : ShellRenderer
    {
        public CustomShellHandler(global::Android.Content.Context context) : base(context) { }

        protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        {
            return new HiddenBottomNavAppearanceTracker();
        }
    }

    internal class HiddenBottomNavAppearanceTracker : IShellBottomNavViewAppearanceTracker
    {
        public void Dispose() { }

        public void ResetAppearance(BottomNavigationView bottomView)
        {
            bottomView.Visibility = ViewStates.Gone;
        }

        public void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            bottomView.Visibility = ViewStates.Gone;
        }
    }
}
#endif