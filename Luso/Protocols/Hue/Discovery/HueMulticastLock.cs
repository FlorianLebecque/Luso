#nullable enable

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Acquires an Android <c>WifiManager.MulticastLock</c> for the duration of the
    /// mDNS scan and releases it when disposed.
    ///
    /// Without this lock, Android filters out incoming multicast packets (including
    /// mDNS responses from the Hue Bridge) while the screen is on Wi-Fi — the bridge
    /// simply never responds to the discovery query.
    ///
    /// On non-Android targets the returned <see cref="IDisposable"/> is a no-op.
    /// </summary>
    internal static partial class HueMulticastLock
    {
        /// <summary>
        /// Acquires the platform multicast lock and returns a handle that releases it on Dispose.
        /// Always call inside a <c>using</c> block around the mDNS scan.
        /// </summary>
        public static partial IDisposable Acquire();
    }
}
