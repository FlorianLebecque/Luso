using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Infrastructure
{
    /// <summary>
    /// Marks an <see cref="ITarget"/> implementation as a self-registering target type
    /// that is discovered via assembly scanning at startup.
    ///
    /// Usage:
    /// <code>
    /// [TargetType("flashlight", TargetKind.Flashlight, "Flashlight")]
    /// internal sealed record FlashlightTarget(...) : ITarget { ... }
    /// </code>
    ///
    /// <see cref="TypeId"/> must be unique across all registrations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class TargetTypeAttribute : Attribute
    {
        /// <summary>Stable machine identifier (e.g. "flashlight", "rgb_light", "vibration").</summary>
        public string TypeId { get; }

        /// <summary>The kind enumeration value this target type maps to.</summary>
        public TargetKind Kind { get; }

        /// <summary>Human-readable display name.</summary>
        public string DisplayName { get; }

        public TargetTypeAttribute(string typeId, TargetKind kind, string displayName)
        {
            TypeId = typeId;
            Kind = kind;
            DisplayName = displayName;
        }
    }
}
