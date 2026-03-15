#nullable enable

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Host-side room announcement lifecycle.
    /// Technologies that do not support broadcast discovery can return null from
    /// <see cref="IRoomTechnology.CreateAnnouncer"/>.
    /// </summary>
    internal interface IRoomAnnouncer : IDisposable
    {
        Task StartAsync();
        Task StopAsync();
    }
}
