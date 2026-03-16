#nullable enable
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Services;
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;

namespace Luso.Shared.Components.Deck.ButtonTypes
{
    /// <summary>Mic button — tap to toggle audio-driven task on/off.</summary>
    internal sealed class MicButtonType : IDeckButtonType
    {
        private static readonly Color ColActive = Color.FromArgb("#FFB900");
        private static readonly Color ColInactive = Color.FromArgb("#383838");

        public string TypeId => "mic";
        public string DisplayName => "Microphone";

        public View BuildView(DeckButtonConfig cfg, DeckButtonContext ctx)
        {
            var label = string.IsNullOrEmpty(cfg.Label) ? "Mic" : cfg.Label;

            // Restore visual state if mic was already active when the pad rebuilds.
            bool isActive = ctx.Orchestrator?.IsRunning(TargetKind.Flashlight) == true
                         && ctx.Orchestrator?.IsRunning(TargetKind.Screen) == true;

            var btn = StrobeButtonType.MakePadButton(label, isActive ? ColActive : ColInactive);
            btn.FontAttributes = FontAttributes.None;
            btn.FontSize = 14;

            btn.Clicked += async (_, _) =>
            {
                if (ctx.Orchestrator is null) return;

                bool nowActive = !ctx.Orchestrator.IsRunning(TargetKind.Flashlight);

                if (nowActive)
                {
                    // Guard: request mic permission on the UI thread before starting.
                    var status = await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var s = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                        if (s != PermissionStatus.Granted)
                            s = await Permissions.RequestAsync<Permissions.Microphone>();
                        return s;
                    });

                    if (status != PermissionStatus.Granted)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                            Application.Current?.MainPage?.DisplayAlert(
                                "Microphone required",
                                "Grant microphone access in Settings to use the mic mode.", "OK"));
                        return;
                    }

                    ctx.Orchestrator.Start(new AudioTask(TargetKind.Flashlight));
                    ctx.Orchestrator.Start(new AudioTask(TargetKind.Screen));
                }
                else
                {
                    ctx.Orchestrator.Stop(TargetKind.Flashlight);
                    ctx.Orchestrator.Stop(TargetKind.Screen);
                }

                btn.BackgroundColor = nowActive ? ColActive : ColInactive;
            };

            return btn;
        }

        public DeckButtonConfig CreateDefault(int row, int col) =>
            new() { TypeId = TypeId, Row = row, Col = col };
    }
}
