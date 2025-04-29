#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace ProtelScanner.Mobile.Platforms.Android
{
    // ProtelScanner.Mobile.Platforms.Android.Services.ForegroundService.cs
    [Service]
    public class ForegroundService : Android.App.Service
    {
        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            // Δημιουργία notification channel για Android 8+
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelId = "protel_scanner_channel";
                var channel = new NotificationChannel(channelId, "Protel Scanner Service", NotificationImportance.Low);
                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);

                var notification = new Notification.Builder(this, channelId)
                    .SetContentTitle("Protel Scanner")
                    .SetContentText("Scanner is running in background")
                    .SetSmallIcon(Resource.Drawable.notification_icon)
                    .Build();

                StartForeground(100, notification);
            }

            return StartCommandResult.Sticky;
        }
    }
}
#endif