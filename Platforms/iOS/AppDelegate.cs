using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using UIKit;
using System;

namespace NBH.Platforms.iOS
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            // Initialisiere das Hauptfenster im transparenten Vollbildmodus
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
                var window = new UIWindow
                {
                    RootViewController = new UIViewController
                    {
                        View = new UIView
                        {
                            BackgroundColor = UIColor.Clear,
                        }
                    }
                };
                window.Frame = UIScreen.MainScreen.Bounds;
                window.MakeKeyAndVisible();
            }

            // Statusleiste ausblenden
            UIApplication.SharedApplication.StatusBarHidden = true;

            // Zugriff auf Handler durch korrekte Namespaces & Verwendung
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("RemoveDoneButton", (handler, view) =>
            {
#if IOS
                if (handler.PlatformView != null)
                    handler.PlatformView.InputAccessoryView = null;
#endif
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("RemoveDoneButton", (handler, view) =>
            {
#if IOS
                if (handler.PlatformView != null)
                    handler.PlatformView.InputAccessoryView = null;
#endif
            });

            return base.FinishedLaunching(app, options);
        }
    }
}
