using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Domain.Targets
{
    /// <summary>
    /// Optional capability interface for targets that can run a strobe pattern natively.
    /// </summary>
    internal interface IStrobeCapableTarget : ITarget
    {
        Task StartStrobeAsync(long atUnixMs, int onMs, int offMs, double frequencyHz);
        Task StopStrobeAsync();
    }
}
