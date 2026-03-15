#nullable enable
using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Domain.Targets
{
    /// <summary>
    /// A binary on/off torch output (device camera flash or LED flashlight).
    /// ExecuteAsync with a <see cref="FlashCommand"/> drives the local hardware directly.
    /// </summary>
    internal sealed record FlashlightTarget(string TargetId) : ITarget
    {
        public TargetKind Kind => TargetKind.Flashlight;
        public string DisplayName => "Flashlight";

        /// <summary>Singleton target ID used for the primary flashlight on a phone.</summary>
        public static readonly FlashlightTarget Default = new("flashlight");

        public async Task ExecuteAsync(FlashCommand command)
        {
            var cmd = command;
            long delayMs = cmd.AtUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (delayMs > 0)
                await Task.Delay((int)Math.Min(delayMs, 5_000));
            try
            {
                if (cmd.Action == FlashAction.On)
                    await Flashlight.Default.TurnOnAsync();
                else
                    await Flashlight.Default.TurnOffAsync();
            }
            catch (FeatureNotSupportedException) { /* flashlight not available on this device */ }
            catch (PermissionException) { /* flashlight permission denied */ }
        }
    }
}
