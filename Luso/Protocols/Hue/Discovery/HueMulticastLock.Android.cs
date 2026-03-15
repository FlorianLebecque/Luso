#if ANDROID
#nullable enable
using Android.Content;
using Android.Net.Wifi;

namespace Luso.Features.Rooms.Networking.Hue
{
    internal static partial class HueMulticastLock
    {
        public static partial IDisposable Acquire()
        {
            var wifiManager = (WifiManager?)
                Android.App.Application.Context.GetSystemService(Context.WifiService);

            if (wifiManager is null)
            {
                System.Diagnostics.Debug.WriteLine("[HueMulticastLock] WifiManager unavailable — skipping lock");
                return NullDisposable.Instance;
            }

            var lockHandle = wifiManager.CreateMulticastLock("luso_hue_mdns");
            lockHandle!.SetReferenceCounted(false);
            lockHandle.Acquire();
            System.Diagnostics.Debug.WriteLine($"[HueMulticastLock] Lock acquired — IsHeld={lockHandle.IsHeld}");

            return new MulticastLockHandle(lockHandle);
        }

        private sealed class MulticastLockHandle : IDisposable
        {
            private readonly WifiManager.MulticastLock _lock;
            public MulticastLockHandle(WifiManager.MulticastLock l) => _lock = l;
            public void Dispose() { if (_lock.IsHeld) _lock.Release(); }
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
#endif
