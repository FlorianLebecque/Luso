#nullable enable

namespace Luso.Features.Rooms.Domain.Technologies
{
    /// <summary>
    /// Factory for all networking operations associated with a specific room technology.
    ///
    /// Implement this interface and decorate the class with
    /// <see cref="Luso.Infrastructure.RoomTechnologyAttribute"/> to self-register:
    /// <code>
    /// [RoomTechnology("ssp", "SSP/1.0", "TCP/UDP local-network rooms", IsDefault = true)]
    /// internal sealed class SspRoomTechnology : IRoomTechnology { ... }
    /// </code>
    ///
    /// <see cref="Luso.Infrastructure.RoomTechnologyRegistry"/> discovers and instantiates
    /// all implementations once via assembly scanning in MauiProgram.
    /// </summary>
    internal interface IRoomTechnology
    {
        /// <summary>Matches the <c>technologyId</c> of <see cref="Luso.Infrastructure.RoomTechnologyAttribute"/>.</summary>
        string TechnologyId { get; }

        // ── Host-role factories ───────────────────────────────────────────────

        /// <summary>
        /// Creates the host-side session for a new room.
        /// Call <see cref="IRoomHostSession.StartAsync"/> to begin accepting connections.
        /// </summary>
        IRoomHostSession? CreateHostSession(Room room);

        /// <summary>
        /// Creates an invite session the host page keeps alive to discover guest devices
        /// and send invites while the room is active.
        /// </summary>
        IInviteSession? CreateInviteSession(Room room);

        /// <summary>
        /// Creates a room announcer for broadcast-style room discovery.
        /// Return <c>null</c> when the technology is invite-only.
        /// </summary>
        IRoomAnnouncer? CreateAnnouncer(Room room);

        // ── Guest-role factories ──────────────────────────────────────────────

        /// <summary>
        /// Creates a scanner the guest page uses to discover rooms and receive invites.
        /// The caller is responsible for calling <see cref="IRoomScanner.StartAsync"/> and Dispose.
        /// </summary>
        IRoomScanner? CreateScanner();

        /// <summary>
        /// Creates a guest session for the discovered room when this technology can join it.
        /// Returns <c>null</c> when guest-join capability is not supported.
        /// </summary>
        IRoomGuestSession? CreateGuestSession(Room room);
    }
}
