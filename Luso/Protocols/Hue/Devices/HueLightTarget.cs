#nullable enable
using HueApi.Models;
using HueApi.Models.Requests;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// <see cref="ITarget"/> for one Philips Hue light bulb.
    ///
    /// Maps <see cref="FlashCommand"/> onto the CLIP v2 light state API:
    ///   <see cref="FlashAction.On"/>  → full-brightness white on
    ///   <see cref="FlashAction.Off"/> → off
    ///
    /// <b>Timing note:</b> <see cref="FlashCommand.AtUnixMs"/> is intentionally ignored
    /// and the command fires immediately. Hue lights have ~100–200 ms LAN latency
    /// and no native future-schedule support; synchronized timing is a future concern.
    ///
    /// Exceptions from the bridge are swallowed to ensure rapid-fire strobe commands
    /// never crash the session.
    /// </summary>
    internal sealed class HueLightTarget : ITarget
    {
        private readonly Guid _lightId;
        private readonly HueBridgeCommandBuffer _buffer;

        public string TargetId { get; }
        public TargetKind Kind => TargetKind.Screen;
        public string DisplayName { get; }

        /// <summary>Transition duration for On commands (ms). 0 = instant.</summary>
        public int TransitionOnMs { get; set; } = 0;
        /// <summary>Transition duration for Off commands (ms). 0 = instant.</summary>
        public int TransitionOffMs { get; set; } = 0;

        internal HueLightTarget(Guid lightId, string displayName, HueBridgeCommandBuffer buffer)
        {
            TargetId = $"hue-light-{lightId}";
            DisplayName = displayName;
            _lightId = lightId;
            _buffer = buffer;
        }

        public Task ExecuteAsync(FlashCommand command)
        {
            // AtUnixMs intentionally ignored — fire immediately.
            UpdateLight update = command.Action == FlashAction.On
                ? new UpdateLight
                {
                    On = new HueApi.Models.On { IsOn = true },
                    Dimming = new Dimming { Brightness = 100 },
                    Dynamics = new HueApi.Models.Dynamics { Duration = TransitionOnMs },
                }
                : new UpdateLight
                {
                    On = new HueApi.Models.On { IsOn = false },
                    Dynamics = new HueApi.Models.Dynamics { Duration = TransitionOffMs },
                };

            _buffer.Schedule(_lightId, update);
            return Task.CompletedTask;
        }
    }
}
