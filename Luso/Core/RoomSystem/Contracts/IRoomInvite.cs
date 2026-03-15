namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// An inbound room invite received by a guest device.
    /// Extends <see cref="IDiscoveredRoom"/> so it can be passed directly to
    /// <c>Room.JoinAsync</c> without unwrapping.
    /// </summary>
    internal interface IRoomInvite : IDiscoveredRoom
    {
        /// <summary>Unique invite identifier used for refusals.</summary>
        string InviteId { get; }

        /// <summary>
        /// True when the invite's protocol version is compatible with this client.
        /// The scanner implementation checks this against its own protocol version.
        /// </summary>
        bool IsCompatible { get; }
    }
}
