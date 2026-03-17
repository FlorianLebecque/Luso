#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;

namespace Luso.Shared.Components.Deck.ButtonTypes
{
    /// <summary>
    /// Target-flash button — hold to flash a specific device target; release to stop.
    ///
    /// Required params: <c>deviceId</c>, <c>targetId</c>.
    /// Optional param:  <c>label</c> overrides the button text (falls back to cfg.Label then targetId).
    ///
    /// Press color comes from <see cref="DeckButtonContext.AccentColor"/> so the DeckPadView
    /// can apply its gradient; falls back to a neutral blue if omitted.
    /// </summary>
    internal sealed class TargetFlashButtonType : IDeckButtonType
    {
        private static readonly Color ColInactive = Color.FromArgb("#383838");
        private static readonly Color ColFallbackActive = Color.FromArgb("#0078D4");

        public string TypeId => "target.flash";
        public string DisplayName => "Target Flash";

        public View BuildView(DeckButtonConfig cfg, DeckButtonContext ctx)
        {
            cfg.Params.TryGetValue("deviceId", out var deviceId);
            cfg.Params.TryGetValue("targetId", out var targetId);

            var label = !string.IsNullOrEmpty(cfg.Label) ? cfg.Label
                      : cfg.Params.TryGetValue("label", out var pl) ? pl
                      : targetId ?? "Target";

            var activeColor = ctx.AccentColor ?? ColFallbackActive;
            var btn = StrobeButtonType.MakePadButton(label, ColInactive);
            btn.FontAttributes = FontAttributes.None;
            btn.FontSize = 12;

            btn.Pressed += (_, _) =>
            {
                btn.BackgroundColor = activeColor;
                if (ctx.Room is null || deviceId is null || targetId is null) return;
                _ = ctx.Room.FlashTargetAsync(deviceId, FlashAction.On, targetId);
            };
            btn.Released += (_, _) =>
            {
                btn.BackgroundColor = ColInactive;
                if (ctx.Room is null || deviceId is null || targetId is null) return;
                _ = ctx.Room.FlashTargetAsync(deviceId, FlashAction.Off, targetId);
            };

            return btn;
        }

        public DeckButtonConfig CreateDefault(int row, int col) =>
            new()
            {
                TypeId = TypeId,
                Row = row,
                Col = col,
                Params = new Dictionary<string, string>
                {
                    ["deviceId"] = string.Empty,
                    ["targetId"] = string.Empty,
                },
            };
    }
}
