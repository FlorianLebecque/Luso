#if !ANDROID
#nullable enable

namespace SyncoStronbo.Services {
    internal static partial class RoomNotifications {
        public static partial void SetAppForeground(bool isForeground) { }
        public static partial void SetGuestStatus(string roomName, string hostIp) { }
        public static partial void SetHostStatus(string roomName, int guestCount) { }
        public static partial void Clear() { }
    }
}
#endif
