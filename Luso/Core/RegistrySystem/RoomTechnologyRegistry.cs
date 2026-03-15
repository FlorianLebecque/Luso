#nullable enable
using System.Reflection;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Infrastructure
{
    /// <summary>
    /// Injectable singleton that owns the technology registry.
    /// Implements <see cref="IRoomTechnologyCatalog"/> so callers can depend on the
    /// interface rather than the concrete static class.
    ///
    /// Call <see cref="ScanAndRegister"/> once at startup (in <c>MauiProgram.cs</c>), then
    /// register the instance as <see cref="IRoomTechnologyCatalog"/> in the DI container.
    /// </summary>
    internal sealed class RoomTechnologyRegistry : IRoomTechnologyCatalog
    {
        private readonly Dictionary<string, IRoomTechnology> _registry = new(StringComparer.OrdinalIgnoreCase);
        private string? _defaultId;

        /// <summary>
        /// Scans the given assembly for all <see cref="RoomTechnologyAttribute"/>-decorated
        /// classes that implement <see cref="IRoomTechnology"/> and registers them.
        /// </summary>
        public void ScanAndRegister(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<RoomTechnologyAttribute>();
                if (attr is null) continue;
                if (!typeof(IRoomTechnology).IsAssignableFrom(type)) continue;

                var instance = (IRoomTechnology)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Could not create instance of {type.FullName}"));

                _registry[attr.TechnologyId] = instance;

                if (attr.IsDefault || _defaultId is null)
                    _defaultId = attr.TechnologyId;
            }
        }

        /// <summary>Returns the technology registered under <paramref name="technologyId"/>.</summary>
        /// <exception cref="KeyNotFoundException">When no technology with that ID is registered.</exception>
        public IRoomTechnology Get(string technologyId)
        {
            if (_registry.TryGetValue(technologyId, out var tech))
                return tech;
            throw new KeyNotFoundException($"No room technology registered for '{technologyId}'. " +
                $"Ensure the class has [RoomTechnology(\"{technologyId}\",...)] and the assembly was scanned.");
        }

        /// <summary>Returns the default technology (the one with <see cref="RoomTechnologyAttribute.IsDefault"/> = true,
        /// or the first one registered if none was marked as default).</summary>
        /// <exception cref="InvalidOperationException">When no technologies have been registered.</exception>
        public IRoomTechnology GetDefault()
        {
            if (_defaultId is null)
                throw new InvalidOperationException("No room technologies have been registered. Call ScanAndRegister first.");
            return _registry[_defaultId];
        }

        /// <summary>Returns all registered technologies.</summary>
        public IReadOnlyCollection<IRoomTechnology> GetAll()
            => _registry.Values;
    }
}
