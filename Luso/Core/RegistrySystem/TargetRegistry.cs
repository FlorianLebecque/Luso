#nullable enable
using System.Reflection;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Infrastructure
{
    /// <summary>
    /// Catalogue of known <see cref="ITarget"/> types discovered via assembly scanning.
    ///
    /// New target types automatically appear in this registry once they are annotated
    /// with <see cref="TargetTypeAttribute"/> — no central switch or factory enum needed.
    ///
    /// Usage in MauiProgram.cs:
    /// <code>
    /// TargetRegistry.ScanAndRegister(Assembly.GetExecutingAssembly());
    /// </code>
    /// </summary>
    internal static class TargetRegistry
    {
        private static readonly Dictionary<string, TargetTypeRegistration> _registry
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Scans the assembly for all <see cref="TargetTypeAttribute"/>-decorated types.</summary>
        public static void ScanAndRegister(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<TargetTypeAttribute>();
                if (attr is null) continue;
                if (!typeof(ITarget).IsAssignableFrom(type)) continue;

                _registry[attr.TypeId] = new TargetTypeRegistration(attr.TypeId, attr.Kind, attr.DisplayName, type);
            }
        }

        /// <summary>Returns all registered target type metadata.</summary>
        public static IReadOnlyCollection<TargetTypeRegistration> GetAll()
            => _registry.Values;

        /// <summary>Returns the registration for a given type ID, or null.</summary>
        public static TargetTypeRegistration? Find(string typeId)
            => _registry.TryGetValue(typeId, out var reg) ? reg : null;
    }

    /// <summary>Metadata for a registered target type.</summary>
    internal sealed class TargetTypeRegistration(
        string TypeId,
        TargetKind Kind,
        string DisplayName,
        Type ClrType
    )
    {
        public string TypeId { get; } = TypeId;
        public TargetKind Kind { get; } = Kind;
        public string DisplayName { get; } = DisplayName;
        public Type ClrType { get; } = ClrType;
    }
}
