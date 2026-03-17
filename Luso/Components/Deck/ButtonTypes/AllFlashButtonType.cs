#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;

namespace Luso.Shared.Components.Deck.ButtonTypes
{
    /// <summary>All-flash button — hold to turn on all flashlights + screens; release to turn off.</summary>
    internal sealed class AllFlashButtonType : IDeckButtonType
    {
        private static readonly Color ColActive = Color.FromArgb("#0078D4");
        private static readonly Color ColInactive = Color.FromArgb("#383838");

        public string TypeId => "all.flash";
        public string DisplayName => "All Flash";

        public View BuildView(DeckButtonConfig cfg, DeckButtonContext ctx)
        {
            var label = string.IsNullOrEmpty(cfg.Label) ? "All" : cfg.Label;
            var btn = StrobeButtonType.MakePadButton(label, ColInactive);

            btn.Pressed += (_, _) =>
            {
                btn.BackgroundColor = ColActive;
                if (ctx.Room is null) return;
                _ = ctx.Room.FlashAsync(FlashAction.On, TargetKind.Flashlight);
                _ = ctx.Room.FlashAsync(FlashAction.On, TargetKind.Screen);
            };
            btn.Released += (_, _) =>
            {
                btn.BackgroundColor = ColInactive;
                if (ctx.Room is null) return;
                _ = ctx.Room.FlashAsync(FlashAction.Off, TargetKind.Flashlight);
                _ = ctx.Room.FlashAsync(FlashAction.Off, TargetKind.Screen);
            };

            return btn;
        }

        public DeckButtonConfig CreateDefault(int row, int col) =>
            new() { TypeId = TypeId, Row = row, Col = col };
    }
}
