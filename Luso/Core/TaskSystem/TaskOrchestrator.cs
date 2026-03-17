#nullable enable
using System.Diagnostics;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Services
{
    internal sealed class TaskOrchestrator : ITaskOrchestrator
    {
        private readonly IRoomSessionStore _session;
        private readonly Dictionary<TargetKind, (ITask Task, CancellationTokenSource Cts)> _running = new();

        public TaskOrchestrator(IRoomSessionStore session) => _session = session;

        public void Start(ITask task)
        {
            Stop(task.Kind);

            if (_session.Current is not { IsHost: true } room)
                return;

            var cts = new CancellationTokenSource();
            _running[task.Kind] = (task, cts);

            _ = Task.Run(async () =>
            {
                try
                {
                    await task.StartAsync(room, cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TaskOrchestrator] Task '{task.GetType().Name}' failed: {ex.Message}");
                }
                finally
                {
                    if (_running.TryGetValue(task.Kind, out var current) && ReferenceEquals(current.Task, task))
                        _running.Remove(task.Kind);
                }
            }, cts.Token);
        }

        public void Stop(TargetKind kind)
        {
            if (!_running.TryGetValue(kind, out var entry))
                return;

            entry.Cts.Cancel();
            entry.Cts.Dispose();
            entry.Task.Stop();
            _running.Remove(kind);
        }

        public void StopAll()
        {
            foreach (var kind in _running.Keys.ToList())
                Stop(kind);
        }

        public bool IsRunning(TargetKind kind) => _running.ContainsKey(kind);

        public void Dispose() => StopAll();
    }
}
