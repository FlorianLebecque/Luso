#nullable enable
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Represents a Philips Hue Bridge discovered on the local network via mDNS.
    ///
    /// Raised through <see cref="IInviteSession.OnDevicePresenceDiscovered"/> by
    /// <see cref="HueInviteSession"/> so the host room page can display it as an
    /// invite candidate.
    ///
    /// <see cref="NeedsPairing"/> is <c>true</c> when no API key is stored for this
    /// bridge yet. The host room page can use this to display a "Press link button"
    /// hint before dispatching the invite. Once the user taps "Invite", the pairing
    /// poll starts automatically inside <see cref="HueInviteSession.SendInviteAsync"/>.
    /// </summary>
    internal sealed class HueDiscoveredDevice : IDiscoveredDevice
    {
        public string DeviceId { get; }
        public string DeviceName { get; }

        /// <summary>The bridge's LAN IP address (used for API calls).</summary>
        public string Address { get; }

        public string TechnologyId => HueRoomTechnology.Id;

        /// <summary>
        /// Non-null when no API key is stored yet. The host page shows this as a
        /// confirmation dialog before calling <c>SendInviteAsync</c> so the user
        /// knows to press the bridge link button first.
        /// </summary>
        public string? PairingHint { get; }

        /// <inheritdoc cref="PairingHint"/>
        internal bool NeedsPairing => PairingHint is not null;

        internal HueDiscoveredDevice(string bridgeId, string ipAddress)
        {
            DeviceId = bridgeId;
            Address = ipAddress;
            DeviceName = $"Hue Bridge ({ipAddress})";
            PairingHint = HueBridgeAuth.GetApiKey(bridgeId) is null
                ? "Press the link button on your Hue Bridge, then tap OK. The app will pair automatically."
                : null;
        }
    }
}
