#nullable enable
using System.Reflection;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Infrastructure
{
    /// <summary>
    /// Provides access to the registered <see cref="IRoomTechnology"/> implementations.
    /// Backed by <see cref="RoomTechnologyRegistry"/>.
    /// </summary>
    internal interface IRoomTechnologyCatalog
    {
        void ScanAndRegister(Assembly assembly);
        IRoomTechnology Get(string technologyId);
        IRoomTechnology GetDefault();
        IReadOnlyCollection<IRoomTechnology> GetAll();
    }
}
