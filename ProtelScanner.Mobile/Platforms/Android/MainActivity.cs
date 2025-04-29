using Android.App;
using Android.Content;         // ← for Intent
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls; // ← for Shell.Current

namespace ProtelScanner.Mobile
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize
                              | ConfigChanges.Orientation
                              | ConfigChanges.UiMode
                              | ConfigChanges.ScreenLayout
                              | ConfigChanges.SmallestScreenSize
                              | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // ← you must have this method signature:
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // only now will Intent and Shell.Current exist in a valid context
            if (Intent?.HasExtra("navigate_to_scanner") == true
                && Intent.GetBooleanExtra("navigate_to_scanner", false))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // make sure you’ve registered "ScannerPage" in your AppShell routes
                    await Shell.Current.GoToAsync(nameof(ScannerPage));
                });
            }
        }
    }
}
