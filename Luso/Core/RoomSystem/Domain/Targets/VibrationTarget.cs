#nullable enable
using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Domain.Targets
{
    /// <summary>
    /// A haptic / vibration motor target.
    /// ExecuteAsync with a <see cref="FlashCommand"/> action "on" triggers a short pulse.
    /// </summary>
    internal sealed record VibrationTarget(string TargetId) : ITarget
    {
        public TargetKind Kind => TargetKind.Vibration;
        public string DisplayName => "Vibration";

        /// <summary>Singleton target ID used for the primary vibration motor on a phone.</summary>
        public static readonly VibrationTarget Default = new("vibration");

        public Task ExecuteAsync(FlashCommand command)
        {
            if (command is { Action: FlashAction.On })
                try { Vibration.Default.Vibrate(200); }
                catch (FeatureNotSupportedException) { /* no vibrator on this device */ }
                catch (PermissionException) { /* VIBRATE permission denied */ }
            return Task.CompletedTask;
        }
    }
}
