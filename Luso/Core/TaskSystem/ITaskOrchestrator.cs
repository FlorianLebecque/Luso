#nullable enable
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Runs host-side tasks and ensures at most one running task per target kind.
    /// </summary>
    internal interface ITaskOrchestrator : IDisposable
    {
        void Start(ITask task);
        void Stop(TargetKind kind);
        void StopAll();
    }
}
