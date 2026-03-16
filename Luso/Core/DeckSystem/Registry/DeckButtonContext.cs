#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Services;

namespace Luso.Shared.Deck.Registry
{
    /// <summary>
    /// Runtime context passed to <see cref="IDeckButtonType.BuildView"/> so button types
    /// can wire up actions without coupling to any page or DI container directly.
    /// Defined as a record so DeckPadView can efficiently create per-cell copies via <c>with</c>.
    /// </summary>
    internal sealed record DeckButtonContext
    {
        /// <summary>The active room. May be null if the deck is rendered outside a room session.</summary>
        public Room? Room { get; init; }

        /// <summary>The task orchestrator for the current host session. May be null for guest views.</summary>
        public ITaskOrchestrator? Orchestrator { get; init; }

        /// <summary>
        /// Per-cell accent color computed by <c>DeckPadView</c> via its gradient algorithm.
        /// Button types that want a position-aware press color read this instead of hardcoding one.
        /// Falls back to the button type's own default if null.
        /// </summary>
        public Color? AccentColor { get; init; }
    }
}
