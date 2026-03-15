namespace Luso.Infrastructure
{
    /// <summary>
    /// Marks a class as a room technology implementation that will be discovered and
    /// registered automatically at application startup via assembly scanning.
    ///
    /// Usage:
    /// <code>
    /// [RoomTechnology("ssp", "SSP/1.0", "TCP/UDP local-network rooms")]
    /// internal sealed class SspRoomTechnology : IRoomTechnology { ... }
    /// </code>
    ///
    /// <see cref="RoomTechnologyId"/> must be unique across all registrations.
    /// Technologies can be disabled at runtime by not annotating the class (comment out the attribute).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class RoomTechnologyAttribute : Attribute
    {
        /// <summary>Stable machine identifier (e.g. "ssp", "hue", "ble").</summary>
        public string TechnologyId { get; }

        /// <summary>Human-readable display name shown in the UI.</summary>
        public string DisplayName { get; }

        /// <summary>Short description of the technology's transport model.</summary>
        public string Description { get; }

        /// <summary>
        /// When true this technology is selected by default when no specific technology
        /// is requested. Only one technology should have this set to true.
        /// </summary>
        public bool IsDefault { get; init; }

        public RoomTechnologyAttribute(string technologyId, string displayName, string description)
        {
            TechnologyId = technologyId;
            DisplayName = displayName;
            Description = description;
        }
    }
}
