#nullable enable
using System.Text.Json.Serialization;

namespace Luso.Shared.Deck.Models
{
    /// <summary>
    /// Top-level container for all <see cref="DeckPage"/> instances belonging to a single
    /// logical deck (e.g. the host pad). Multiple layouts are possible in future
    /// (different rooms, different profiles).
    /// </summary>
    public sealed class DeckLayout
    {
        /// <summary>Stable unique identifier. Used as the JSON file name on disk.</summary>
        [JsonPropertyName("layoutId")]
        public string LayoutId { get; set; } = "default";

        /// <summary>Ordered list of pages; first page is shown on load.</summary>
        [JsonPropertyName("pages")]
        public List<DeckPage> Pages { get; set; } = new();
    }
}
