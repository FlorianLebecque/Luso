#nullable enable
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;

namespace Luso.Shared.Components.Deck
{
    /// <summary>
    /// A pluggable button implementation for the deck pad.
    ///
    /// Implementations live in <c>Components/Deck/ButtonTypes/</c> and are registered
    /// at startup via <see cref="IDeckButtonRegistry.Register"/>.
    ///
    /// <para>Dependency rules:</para>
    /// <list type="bullet">
    ///   <item>May read <see cref="DeckButtonConfig.Params"/> for type-specific configuration.</item>
    ///   <item>May use <see cref="DeckButtonContext.Room"/> and <see cref="DeckButtonContext.Orchestrator"/>
    ///     for runtime wiring.</item>
    ///   <item>Must never capture a reference to the containing page.</item>
    /// </list>
    /// </summary>
    internal interface IDeckButtonType
    {
        /// <summary>Stable identifier used in <see cref="DeckButtonConfig.TypeId"/> (e.g. "strobe", "mic", "target.flash").</summary>
        string TypeId { get; }

        /// <summary>Human-readable name shown in the button-picker editor.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Builds the live MAUI <see cref="View"/> for this button given its config and runtime context.
        /// The returned view is placed directly inside the deck grid cell.
        /// </summary>
        View BuildView(DeckButtonConfig cfg, DeckButtonContext ctx);

        /// <summary>
        /// Returns a fresh <see cref="DeckButtonConfig"/> with sensible defaults for this type,
        /// positioned at the given slot. Used when the user adds a new button in edit mode.
        /// </summary>
        DeckButtonConfig CreateDefault(int row, int col);
    }
}
