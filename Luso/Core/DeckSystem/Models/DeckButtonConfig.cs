#nullable enable
using System.Text.Json.Serialization;

namespace Luso.Shared.Deck.Models
{
    /// <summary>
    /// Serializable configuration for a single button slot in a <see cref="DeckPage"/>.
    ///
    /// No MAUI types here — this is pure data.
    /// A registered <c>IDeckButtonType</c> reads <see cref="Params"/> to build the live view.
    /// </summary>
    public sealed class DeckButtonConfig
    {
        /// <summary>Stable unique identifier for this button within its page.</summary>
        [JsonPropertyName("buttonId")]
        public string ButtonId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>Registered type identifier (e.g. "strobe", "mic", "target.flash").</summary>
        [JsonPropertyName("typeId")]
        public string TypeId { get; init; } = string.Empty;

        /// <summary>Human-readable label override. If empty the type's default label is used.</summary>
        [JsonPropertyName("label")]
        public string Label { get; init; } = string.Empty;

        /// <summary>Zero-based row in the page grid.</summary>
        [JsonPropertyName("row")]
        public int Row { get; init; }

        /// <summary>Zero-based column in the page grid.</summary>
        [JsonPropertyName("col")]
        public int Col { get; init; }

        /// <summary>How many rows this button spans (default 1).</summary>
        [JsonPropertyName("rowSpan")]
        public int RowSpan { get; init; } = 1;

        /// <summary>How many columns this button spans (default 1).</summary>
        [JsonPropertyName("colSpan")]
        public int ColSpan { get; init; } = 1;

        /// <summary>Type-specific configuration values (e.g. targetId, bpm).</summary>
        [JsonPropertyName("params")]
        public Dictionary<string, string> Params { get; init; } = new();

        /// <summary>Creates a copy with updated position.</summary>
        public DeckButtonConfig WithSlot(int row, int col) =>
            new DeckButtonConfig
            {
                ButtonId = ButtonId,
                TypeId = TypeId,
                Label = Label,
                Row = row,
                Col = col,
                RowSpan = RowSpan,
                ColSpan = ColSpan,
                Params = Params,
            };
    }
}
