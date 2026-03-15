#nullable enable

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Guest-side network scanner.
    ///
    /// Finds available rooms and handles incoming invites from hosts on the same network.
    /// Created by <see cref="IRoomTechnology.CreateScanner"/> and owned by the guest page.
    ///
    /// Lifecycle:
    ///   CreateScanner() → Start() → (events fire) → Stop() → Dispose()
    /// </summary>
    internal interface IRoomScanner : IDisposable
    {
        /// <summary>Raised on each newly discovered room advertisement.</summary>
        event EventHandler<IDiscoveredRoom>? OnRoomDiscovered;

        /// <summary>Raised when the host sends a direct invite to this device.</summary>
        event EventHandler<IRoomInvite>? OnInviteReceived;

        /// <summary>Starts listening for room advertisements and incoming invites.</summary>
        Task StartAsync();

        /// <summary>Stops all network activity. Does not dispose the scanner.</summary>
        void Stop();

        /// <summary>
        /// Sends a refusal for the given invite back to the host.
        /// Called automatically by the scanner when <see cref="IRoomInvite.IsCompatible"/> is false;
        /// also callable by the page when the user declines.
        /// </summary>
        Task RefuseInviteAsync(IRoomInvite invite, string reason);
    }
}
