namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Protocol-agnostic representation of an available room discovered on the network.
    ///
    /// Concrete implementations wrap protocol-specific discovery payloads
    /// (e.g. SSP ANNC datagrams, Bluetooth LE advertisements, etc.).
    /// </summary>
    internal interface IDiscoveredRoom
    {
        /// <summary>The stable room identifier.</summary>
        string RoomId { get; }

        /// <summary>Human-readable room name set by the host.</summary>
        string RoomName { get; }

        /// <summary>
        /// The technology ID that discovered this room.
        /// Used by <see cref="Luso.Infrastructure.RoomTechnologyRegistry"/> to look up
        /// the correct <see cref="IRoomTechnology"/> for joining.
        /// </summary>
        string TechnologyId { get; }
    }
}
