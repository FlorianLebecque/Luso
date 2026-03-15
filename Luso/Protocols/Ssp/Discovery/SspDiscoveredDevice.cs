using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Adapts a <see cref="GuestPresenceAnnouncement"/> (SSP PRES wire type) to the
    /// protocol-agnostic <see cref="IDiscoveredDevice"/> interface.
    /// </summary>
    internal sealed class SspDiscoveredDevice : IDiscoveredDevice
    {
        private readonly GuestPresenceAnnouncement _pres;

        public string DeviceId => _pres.GuestId;
        public string DeviceName => _pres.GuestName;
        public string Address => _pres.GuestIp;
        public string TechnologyId => SspRoomTechnology.Id;
        public string? PairingHint => null;

        /// <summary>Raw IP, kept internal for the invite sender.</summary>
        internal string GuestIp => _pres.GuestIp;

        public SspDiscoveredDevice(GuestPresenceAnnouncement pres) => _pres = pres;
    }
}
