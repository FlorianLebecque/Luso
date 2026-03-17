namespace Luso.Features.Rooms.Networking.Ssp
{
    internal static class GuestIdentity
    {
        private const string GuestIdKey = "room.guest.id";

        public static string GetOrCreateGuestId()
        {
            var id = Microsoft.Maui.Storage.Preferences.Get(GuestIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id)) return id;
            id = Guid.NewGuid().ToString("N");
            Microsoft.Maui.Storage.Preferences.Set(GuestIdKey, id);
            return id;
        }

        public static string DeviceName()
            => Microsoft.Maui.Devices.DeviceInfo.Current.Name;

        public static string LocalIpv4()
        {
            try
            {
                static bool IsPreferredLan(System.Net.NetworkInformation.NetworkInterface iface)
                    => iface.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211
                        or System.Net.NetworkInformation.NetworkInterfaceType.Ethernet;

                static bool IsLikelyVirtual(System.Net.NetworkInformation.NetworkInterface iface)
                {
                    string n = iface.Name.ToLowerInvariant();
                    string d = iface.Description.ToLowerInvariant();
                    return n.Contains("docker") || n.Contains("veth") || n.Contains("br-") || n.Contains("wg") ||
                           d.Contains("docker") || d.Contains("hyper-v") || d.Contains("virtual") || d.Contains("vpn") || d.Contains("tunnel");
                }

                var interfaces = System.Net.NetworkInformation.NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(iface => iface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .OrderByDescending(IsPreferredLan)
                    .ThenBy(iface => IsLikelyVirtual(iface));

                foreach (var iface in interfaces)
                {
                    foreach (var ip in iface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        if (System.Net.IPAddress.IsLoopback(ip.Address)) continue;
                        return ip.Address.ToString();
                    }
                }
            }
            catch
            {
                // Network interface enumeration failed — fall back to loopback.
            }
            return "127.0.0.1";
        }
    }
}
