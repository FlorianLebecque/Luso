#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Infrastructure;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Hue Bridge room technology.
    ///
    /// Self-registers via <see cref="RoomTechnologyAttribute"/> at application startup.
    ///
    /// <b>Role:</b> Hue is an invite-only device provider — it does not create or host
    /// SSP-style rooms, nor can Android join a room "as" a Hue bridge. The technology
    /// contributes:
    ///   <list type="bullet">
    ///     <item><see cref="HueHostSession"/> — passive tracker for paired bridges.</item>
    ///     <item><see cref="HueInviteSession"/> — mDNS discovery + link-button pairing.</item>
    ///   </list>
    /// All other factory methods return <c>null</c> to signal no-op to
    /// <see cref="Luso.Features.Rooms.RoomFactory"/>.
    ///
    /// <b>Wiring note:</b> <see cref="RoomFactory"/> calls <see cref="CreateHostSession"/>
    /// then <see cref="CreateInviteSession"/> in sequence for the same room build.
    /// <see cref="_pendingHostSession"/> bridges that call sequence so the invite session
    /// can forward paired devices into the host session without any external coordination.
    /// </summary>
    [RoomTechnology(Id, "Hue Bridge", "Philips Hue smart lights via local CLIP v2")]
    internal sealed class HueRoomTechnology : IRoomTechnology
    {
        public const string Id = "hue";
        public string TechnologyId => Id;

        // Holds the host session across the two sequential factory calls made by RoomFactory.
        private HueHostSession? _pendingHostSession;

        // ── Host-role factories ───────────────────────────────────────────────

        public IRoomHostSession? CreateHostSession(Room room)
        {
            _pendingHostSession = new HueHostSession();
            return _pendingHostSession;
        }

        public IInviteSession? CreateInviteSession(Room room)
        {
            var host = _pendingHostSession;
            _pendingHostSession = null;   // consumed
            return host is null ? null : new HueInviteSession(host);
        }

        public IRoomAnnouncer? CreateAnnouncer(Room room) => null;

        // ── Guest-role factories (not applicable for Hue) ─────────────────────

        public IRoomScanner? CreateScanner() => null;

        public IRoomGuestSession? CreateGuestSession(Room room) => null;
    }
}
