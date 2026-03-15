#nullable enable
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Domain.Devices
{
    /// <summary>
    /// Protocol-agnostic representation of a participant device in a room session.
    ///
    /// A device is any controllable endpoint, regardless of how it is connected:
    /// an SSP phone guest, a Hue bridge, a local device acting as host, etc.
    ///
    /// Each device exposes a list of <see cref="ITarget"/>s — the concrete output
    /// endpoints it supports. Commands are directed at individual targets rather than
    /// at the device as a whole, which enables capability-aware dispatch.
    ///
    /// Connection lifecycle is tracked through <see cref="Status"/> and the
    /// <see cref="OnStatusChanged"/> event.
    ///
    /// For networked devices the <see cref="LatencyMs"/> property carries the last
    /// measured round-trip time (-1 when not yet measured).
    /// </summary>
    internal interface IDevice
    {
        // ── Identity ──────────────────────────────────────────────────────────

        /// <summary>Stable identifier for this device within the session.
        /// For SSP devices this is the guest IP; for Hue it may be the bridge ID + light id.</summary>
        string DeviceId { get; }

        /// <summary>Human-readable name to display in the UI.</summary>
        string DeviceName { get; }

        // ── State ─────────────────────────────────────────────────────────────

        /// <summary>Current connection/session state of this device.</summary>
        DeviceStatus Status { get; }

        /// <summary>Last measured round-trip latency in milliseconds, or -1 if unknown.</summary>
        int LatencyMs { get; }

        // ── Targets ───────────────────────────────────────────────────────────

        /// <summary>
        /// Output endpoints exposed by this device.
        /// The list is populated once capabilities are known (i.e. when <see cref="Status"/>
        /// reaches <see cref="DeviceStatus.Ready"/>).
        /// </summary>
        IReadOnlyList<ITarget> Targets { get; }

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
        event EventHandler<DeviceStatus>? OnStatusChanged;

        /// <summary>Raised whenever <see cref="LatencyMs"/> is updated (e.g. after a ping/pong).</summary>
        event EventHandler<int>? OnLatencyUpdated;

        /// <summary>
        /// Removes this device from the session (host perspective: kicks the device).
        /// For <see cref="Luso.Features.Rooms.Domain.Devices.LocalDevice"/> this is a no-op.
        /// </summary>
        Task DisconnectAsync();
    }
}
