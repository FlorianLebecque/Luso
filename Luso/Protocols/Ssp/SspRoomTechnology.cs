#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Infrastructure;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// SSP/1.0 room technology implementation.
    ///
    /// This class self-registers via <see cref="RoomTechnologyAttribute"/>:
    /// at application startup <see cref="Luso.Infrastructure.RoomTechnologyRegistry.ScanAndRegister"/>
    /// discovers this class, creates a single instance, and registers it under
    /// <see cref="Id"/> = "ssp".
    ///
    /// No other file needs to reference this class by name. Adding a new technology
    /// (e.g. "hue", "ble") is as simple as creating an analogous class in a new folder
    /// and annotating it with <c>[RoomTechnology("hue", ...)]</c>.
    /// </summary>
    [RoomTechnology(Id, "SSP/1.0", "TCP/UDP local-area-network rooms", IsDefault = true)]
    internal sealed class SspRoomTechnology : IRoomTechnology
    {
        /// <summary>Stable technology identifier used throughout the domain layer.</summary>
        public const string Id = "ssp";

        public string TechnologyId => Id;

        // ── Host-role factories ───────────────────────────────────────────────

        public IRoomHostSession? CreateHostSession(Room room)
            => new SspHostSession(room.RoomId, room.RoomName);

        public IInviteSession? CreateInviteSession(Room room)
            => new SspInviteSession(room.RoomId, room.RoomName);

        public IRoomAnnouncer? CreateAnnouncer(Room room)
            => new SspRoomAnnouncer(room.RoomId, room.RoomName, SocketRoomHost.TcpPort);

        // ── Guest-role factories ──────────────────────────────────────────────

        public IRoomScanner? CreateScanner()
            => new SspRoomScanner(GuestIdentity.GetOrCreateGuestId(), GuestIdentity.DeviceName());

        public IRoomGuestSession? CreateGuestSession(Room room)
        {
            if (room.SourceDiscovery is null)
                return null;

            RoomAnnouncement ann = room.SourceDiscovery switch
            {
                SspDiscoveredRoom d => new RoomAnnouncement(d.RoomId, d.RoomName, d.HostIp, d.TcpPort),
                SspRoomInvite i => i.AsAnnouncement(),
                _ => throw new ArgumentException(
                    $"Cannot join a '{room.SourceDiscovery.TechnologyId}' room with the SSP technology adapter.")
            };

            return new SspGuestSession(ann, room.LocalDevice!);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Converts the domain <see cref="LocalDevice"/> targets back into the SSP wire
        /// capabilities format that the JOIN message requires.
        /// </summary>
        internal static GuestCapabilities BuildCapabilities(LocalDevice local)
        {
            bool hasFlash = local.Targets.Any(t => t is Domain.Targets.FlashlightTarget);
            bool hasScreen = local.Targets.FirstOrDefault(t => t is Domain.Targets.ScreenTarget)
                             is Domain.Targets.ScreenTarget rgb
                ? rgb.HasResolution : false;
            bool hasVib = local.Targets.Any(t => t is Domain.Targets.VibrationTarget);

            int w = 0, h = 0;
            if (local.Targets.FirstOrDefault(t => t is Domain.Targets.ScreenTarget) is Domain.Targets.ScreenTarget s)
            {
                w = s.PixelWidth;
                h = s.PixelHeight;
            }

            return new GuestCapabilities(
                HasFlashlight: hasFlash,
                HasVibration: hasVib,
                HasScreen: hasScreen || (w > 0 && h > 0),
                ScreenWidth: w,
                ScreenHeight: h);
        }
    }
}
