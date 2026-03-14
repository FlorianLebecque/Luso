namespace SyncoStronbo.Features.Rooms.Networking {
    internal static class GuestIdentity {
        private const string GuestIdKey = "room.guest.id";

        public static string GetOrCreateGuestId() {
            var id = Microsoft.Maui.Storage.Preferences.Get(GuestIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id)) return id;
            id = Guid.NewGuid().ToString("N");
            Microsoft.Maui.Storage.Preferences.Set(GuestIdKey, id);
            return id;
        }

        public static string DeviceName()
            => Microsoft.Maui.Devices.DeviceInfo.Current.Name;

        public static string LocalIpv4() {
            try {
                foreach (var iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                    if (iface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var ip in iface.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !System.Net.IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            } catch {
            }
            return "127.0.0.1";
        }
    }
}
