#nullable enable

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Host-side invite session.
    ///
    /// Handles discovering nearby guest devices via presence broadcasts and
    /// sending invites to selected candidates.
    ///
    /// Created by <see cref="IRoomTechnology.CreateInviteSession"/> and owned by the host page.
    ///
    /// Lifecycle:
    ///   CreateInviteSession(...) → Start() → (events fire) → Stop() → Dispose()
    /// </summary>
    internal interface IInviteSession : IDisposable
    {
        /// <summary>Raised when a guest device broadcasts its presence.</summary>
        event EventHandler<IDiscoveredDevice>? OnDevicePresenceDiscovered;

        /// <summary>Raised when an invited device explicitly refuses the invite.</summary>
        event EventHandler<string>? OnInviteRefused;   // payload = inviteId

        /// <summary>Starts listening for presence broadcasts and invite refusals.</summary>
        void Start();

        /// <summary>Stops all listening. Does not dispose the session.</summary>
        void Stop();

        /// <summary>Sends an invite to the given device to join <paramref name="roomId"/>.</summary>
        Task SendInviteAsync(IDiscoveredDevice device, string roomId, string roomName);
    }
}
