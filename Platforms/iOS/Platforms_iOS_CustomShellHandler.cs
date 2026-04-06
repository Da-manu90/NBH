// ============================================================
// CustomShellHandler.cs – iOS TabBar ausblenden
// ============================================================
// Pfad: /NBH/Platforms/iOS/CustomShellHandler.cs
//
// Garantiert dass die native iOS UITabBar komplett ausgeblendet
// wird, auch wenn Shell.TabBarIsVisible="False" nicht greift.
//
// REGISTRIERUNG in MauiProgram.cs (zusammen mit Android):
//   builder.ConfigureMauiHandlers(handlers =>
//   {
// #if ANDROID
//       handlers.AddHandler<Shell, NBH.Platforms.Android.CustomShellHandler>();
// #elif IOS
//       handlers.AddHandler<Shell, NBH.Platforms.iOS.CustomShellHandler>();
// #endif
//   });
// ============================================================

#if IOS
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

namespace NBH.Platforms.iOS
{
    public class CustomShellHandler : ShellRenderer
    {
        protected override IShellTabBarAppearanceTracker CreateTabBarAppearanceTracker()
        {
            return new HiddenTabBarAppearanceTracker();
        }
    }

    internal class HiddenTabBarAppearanceTracker : IShellTabBarAppearanceTracker
    {
        public void Dispose() { }

        public void ResetAppearance(UITabBarController controller)
        {
            if (controller?.TabBar != null)
            {
                controller.TabBar.Hidden = true;
                controller.TabBar.Frame = new CoreGraphics.CGRect(0, 0, 0, 0);
            }
        }

        public void SetAppearance(UITabBarController controller, ShellAppearance appearance)
        {
            if (controller?.TabBar != null)
            {
                controller.TabBar.Hidden = true;
                controller.TabBar.Frame = new CoreGraphics.CGRect(0, 0, 0, 0);
            }
        }

        public void UpdateLayout(UITabBarController controller)
        {
            if (controller?.TabBar != null)
            {
                controller.TabBar.Hidden = true;
            }
        }
    }
}
#endif
