namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// A remote device discovered via a presence-broadcast mechanism (e.g. SSP PRES
    /// datagrams). Used by the host-side invite flow to show candidate devices.
    /// </summary>
    internal interface IDiscoveredDevice
    {
        /// <summary>Stable identifier for the discovered device (e.g. IP address in SSP).</summary>
        string DeviceId { get; }

        /// <summary>Human-readable name of the device.</summary>
        string DeviceName { get; }

        /// <summary>Network address used to send an invite or make a connection.</summary>
        string Address { get; }

        /// <summary>
        /// Identifies which <see cref="Luso.Infrastructure.RoomTechnologyRegistry"/> entry
        /// owns this device. Used by <c>Room.SendInviteAsync</c> to route the invite through
        /// the correct <see cref="IInviteSession"/>.
        /// </summary>
        string TechnologyId { get; }

        /// <summary>
        /// When non-null, the host page must show this instruction to the user and
        /// wait for confirmation before calling <c>Room.SendInviteAsync</c>.
        /// Returns <c>null</c> for technologies that require no manual pairing step.
        /// </summary>
        string? PairingHint { get; }
    }
}
