#nullable enable
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Domain.Devices
{
    /// <summary>
    /// Represents the local device (the one running this app) as a room participant.
    ///
    /// For a Host this is always present in the session. Its targets are detected at
    /// runtime from the platform's available hardware.
    ///
    /// <see cref="LatencyMs"/> is always 0 for the local device (no network hop).
    /// </summary>
    internal sealed class LocalDevice : IDevice
    {
        public string DeviceId { get; }
        public string DeviceName { get; }
        public DeviceStatus Status { get; private set; } = DeviceStatus.Ready;
        public int LatencyMs => 0;

        public IReadOnlyList<ITarget> Targets { get; }

        public event EventHandler<DeviceStatus>? OnStatusChanged;
        public event EventHandler<int>? OnLatencyUpdated;

        public LocalDevice(string deviceId, string deviceName, IReadOnlyList<ITarget> targets)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            Targets = targets;
        }

        /// <summary>
        /// Builds a <see cref="LocalDevice"/> from the current platform capabilities.
        /// </summary>
        public static LocalDevice Detect()
        {
            var targets = new List<ITarget>
            {
                // Flashlight is assumed available on Android phones; hardware-level availability
                // is checked at execution time rather than removed from the target list.
                FlashlightTarget.Default
            };

            var display = Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo;
            if (display.Width > 0 && display.Height > 0)
                targets.Add(ScreenTarget.Screen((int)display.Width, (int)display.Height));

            targets.Add(VibrationTarget.Default);

            string id = Microsoft.Maui.Devices.DeviceInfo.Current.Idiom.ToString()
                          + "_local";
            string name = Microsoft.Maui.Devices.DeviceInfo.Current.Name;

            return new LocalDevice(id, name, targets.AsReadOnly());
        }

        /// <summary>The local device cannot be disconnected remotely.</summary>
        public Task DisconnectAsync() => Task.CompletedTask;
    }
}
