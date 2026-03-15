namespace Luso.Features.Rooms.Domain.Devices
{
    /// <summary>
    /// Lifecycle state of a device within the room session.
    /// </summary>
    internal enum DeviceStatus
    {
        /// <summary>Device is known but connection state is undetermined.</summary>
        Unknown,

        /// <summary>Transport-level connection is being established.</summary>
        Connecting,

        /// <summary>Protocol handshake completed; device is active in the session.</summary>
        Connected,

        /// <summary>Capabilities confirmed; device is ready to receive commands.</summary>
        Ready,

        /// <summary>Device is in the process of leaving the session.</summary>
        Disconnecting,

        /// <summary>Device has left or been removed from the session.</summary>
        Disconnected,
    }
}
