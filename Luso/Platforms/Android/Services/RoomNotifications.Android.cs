#if ANDROID
#nullable enable
using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace Luso.Features.Rooms.Services {
    internal static partial class RoomNotifications {
        private const string ChannelId = "luso_room_status";
        private const int NotificationId = 13000;

        private static bool _isForeground = true;
        private static string? _title;
        private static string? _text;

        public static partial void SetAppForeground(bool isForeground) {
            _isForeground = isForeground;
            Refresh();
        }

        public static partial void SetGuestStatus(string roomName, string hostIp) {
            _title = "Connected as guest";
            _text = $"Room '{roomName}' • Host {hostIp}";
            Refresh();
        }

        public static partial void SetHostStatus(string roomName, int guestCount) {
            string guests = guestCount == 1 ? "1 guest" : $"{guestCount} guests";
            _title = "Hosting room";
            _text = $"Room '{roomName}' • {guests} connected";
            Refresh();
        }

        public static partial void Clear() {
            _title = null;
            _text = null;
            CancelNotification();
        }

        private static void Refresh() {
            if (_title is null || _text is null || _isForeground) {
                CancelNotification();
                return;
            }

            ShowNotification(_title, _text);
        }

        private static void ShowNotification(string title, string text) {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            if (context is null) return;

            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            if (manager is null) return;

            EnsureChannel(manager);

            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
            PendingIntent? pendingIntent = null;
            if (intent is not null) {
                intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                pendingIntent = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            }

            var builder = new Notification.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(text)
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                .SetOngoing(true)
                .SetOnlyAlertOnce(true)
                .SetVisibility(NotificationVisibility.Public)
                .SetCategory(Notification.CategoryStatus);

            if (pendingIntent is not null)
                builder.SetContentIntent(pendingIntent);

            manager.Notify(NotificationId, builder.Build());
        }

        private static void CancelNotification() {
            var context = Platform.CurrentActivity ?? Android.App.Application.Context;
            if (context is null) return;

            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            manager?.Cancel(NotificationId);
        }

        private static void EnsureChannel(NotificationManager manager) {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

            var existing = manager.GetNotificationChannel(ChannelId);
            if (existing is not null) return;

            var channel = new NotificationChannel(ChannelId, "Room status", NotificationImportance.Low) {
                Description = "Shows room connectivity status while app is in background"
            };
            manager.CreateNotificationChannel(channel);
        }
    }
}
#endif
