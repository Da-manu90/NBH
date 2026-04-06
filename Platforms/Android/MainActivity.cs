using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui.ApplicationModel;

namespace NBH
{
    [Activity(
        Theme = "@style/SplashTheme", // Dein SplashScreen-Theme
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize |
                                ConfigChanges.Orientation |
                                ConfigChanges.UiMode |
                                ConfigChanges.ScreenLayout |
                                ConfigChanges.SmallestScreenSize |
                                ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Window?.SetSoftInputMode(SoftInput.AdjustResize);

            // Ab Android 12: Deaktiviere das Standard-Launcher-Icon
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                // Verhindert, dass das Launcher-Icon vor deinem SplashScreen angezeigt wird
                Window?.SetBackgroundDrawableResource(Android.Resource.Color.Transparent);
            }
        }
    }
}
