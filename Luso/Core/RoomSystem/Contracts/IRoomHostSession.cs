#nullable enable
using Luso.Features.Rooms.Domain.Devices;

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Host-side session — pure lifecycle.
    ///
    /// Responsible for accepting incoming device connections and maintaining them.
    /// Once a device connects it is modelled as an <see cref="IDevice"/> whose
    /// <see cref="Luso.Features.Rooms.Domain.Targets.ITarget"/>s handle all command
    /// dispatch. This session never relays commands itself.
    /// </summary>
    internal interface IRoomHostSession : IDisposable
    {
        /// <summary>Starts accepting connections.</summary>
        Task StartAsync();

        /// <summary>Stops accepting new connections.</summary>
        Task StopAsync();

        /// <summary>Disconnects all guests and closes the session.</summary>
        Task CloseAsync();

        /// <summary>Raised when a guest completes the join handshake and is ready.</summary>
        event EventHandler<IDevice>? OnGuestConnected;

        /// <summary>Raised when a guest leaves or is removed from the session.</summary>
        event EventHandler<IDevice>? OnGuestDisconnected;

        /// <summary>Raised when a guest's round-trip latency is updated.</summary>
        event EventHandler<IDevice>? OnGuestLatencyUpdated;

        /// <summary>Number of currently connected devices.</summary>
        int DeviceCount { get; }

        /// <summary>Returns a snapshot of all connected devices.</summary>
        IReadOnlyList<IDevice> GetDevices();
    }
}
