#nullable enable
using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Guest-side session — pure lifecycle.
    ///
    /// Responsible for maintaining the connection to the host and raising
    /// lifecycle events. Command execution is dispatched to the local device's
    /// targets, and the <see cref="OnFlashCommand"/> event fires for each
    /// received command.
    /// </summary>
    internal interface IRoomGuestSession : IDisposable
    {
        /// <summary>Connects to the host and starts the session.</summary>
        Task StartAsync();

        /// <summary>Stops the session without leaving. Can be restarted.</summary>
        Task StopAsync();

        /// <summary>Leaves the room gracefully and disconnects.</summary>
        Task LeaveAsync();

        /// <summary>Raised when the host closes the room or the connection is lost.</summary>
        event EventHandler? OnHostDisconnected;

        /// <summary>Raised when the host explicitly kicks this device.</summary>
        event EventHandler? OnKicked;

        /// <summary>Raised when a flash command is received from the host.</summary>
        event EventHandler<FlashCommand>? OnFlashCommand;
    }
}
