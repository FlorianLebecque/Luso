#if !ANDROID
#nullable enable

namespace Luso.Features.Rooms.Networking.Hue
{
    internal static partial class HueMulticastLock
    {
        public static partial IDisposable Acquire() => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
#endif
