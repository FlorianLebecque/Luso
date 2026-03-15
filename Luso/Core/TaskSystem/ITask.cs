#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// A host-side runnable task scoped to a specific target kind.
    /// </summary>
    internal interface ITask
    {
        TargetKind Kind { get; }
        Task StartAsync(Room room, CancellationToken cancellationToken);
        void Stop();
    }
}
