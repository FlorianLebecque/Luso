#nullable enable
using System.Text.Json.Serialization;

namespace Luso.Shared.Deck.Models
{
    /// <summary>
    /// A named grid page within a <see cref="DeckLayout"/>.
    /// Defines its own row/column count and contains an ordered list of button configs.
    /// </summary>
    public sealed class DeckPage
    {
        /// <summary>Stable unique identifier for this page.</summary>
        [JsonPropertyName("pageId")]
        public string PageId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Display name shown on the tab bar.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Page";

        /// <summary>Number of grid rows.</summary>
        [JsonPropertyName("rows")]
        public int Rows { get; set; } = 2;

        /// <summary>Number of grid columns.</summary>
        [JsonPropertyName("cols")]
        public int Cols { get; set; } = 3;

        /// <summary>Buttons on this page (unsorted — position is defined by Row/Col on each config).</summary>
        [JsonPropertyName("buttons")]
        public List<DeckButtonConfig> Buttons { get; set; } = new();
    }
}
