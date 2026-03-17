#nullable enable
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Services;
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;

namespace Luso.Shared.Components.Deck.ButtonTypes
{
    /// <summary>Strobe button — hold to strobe flashlight + screen; release to stop.</summary>
    internal sealed class StrobeButtonType : IDeckButtonType
    {
        private static readonly Color ColActive = Color.FromArgb("#0078D4");
        private static readonly Color ColInactive = Color.FromArgb("#383838");

        public string TypeId => "strobe";
        public string DisplayName => "Strobe";

        public View BuildView(DeckButtonConfig cfg, DeckButtonContext ctx)
        {
            var label = string.IsNullOrEmpty(cfg.Label) ? "Strobe" : cfg.Label;
            var btn = MakePadButton(label, ColInactive);

            btn.Pressed += (_, _) =>
            {
                btn.BackgroundColor = ColActive;
                ctx.Orchestrator?.Start(new StrobeTask(TargetKind.Flashlight, 10));
                ctx.Orchestrator?.Start(new StrobeTask(TargetKind.Screen, 10));
            };
            btn.Released += (_, _) =>
            {
                btn.BackgroundColor = ColInactive;
                ctx.Orchestrator?.Stop(TargetKind.Flashlight);
                ctx.Orchestrator?.Stop(TargetKind.Screen);
            };

            return btn;
        }

        public DeckButtonConfig CreateDefault(int row, int col) =>
            new() { TypeId = TypeId, Row = row, Col = col };

        internal static Button MakePadButton(string text, Color bg) => new()
        {
            Text = text,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            BackgroundColor = bg,
            TextColor = Colors.White,
            CornerRadius = 14,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
    }
}
