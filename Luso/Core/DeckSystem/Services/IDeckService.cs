#nullable enable
using Luso.Shared.Deck.Models;

namespace Luso.Shared.Deck.Services
{
    /// <summary>
    /// CRUD operations over a <see cref="DeckLayout"/> plus JSON persistence.
    /// </summary>
    public interface IDeckService
    {
        /// <summary>Returns the layout for <paramref name="layoutId"/>, loading from disk or creating a fresh one.</summary>
        Task<DeckLayout> GetLayoutAsync(string layoutId = "default");

        /// <summary>Persists the given layout to disk.</summary>
        Task SaveAsync(DeckLayout layout);

        /// <summary>Adds a new page to the layout and saves.</summary>
        Task<DeckPage> AddPageAsync(DeckLayout layout, string name, int rows = 2, int cols = 3);

        /// <summary>Removes a page by id and saves. Silently ignores unknown ids.</summary>
        Task RemovePageAsync(DeckLayout layout, string pageId);

        /// <summary>Adds a button config to a page and saves.</summary>
        Task AddButtonAsync(DeckLayout layout, DeckPage page, DeckButtonConfig config);

        /// <summary>Removes a button by id from a page and saves. Silently ignores unknown ids.</summary>
        Task RemoveButtonAsync(DeckLayout layout, DeckPage page, string buttonId);

        /// <summary>Replaces a button config in-place and saves.</summary>
        Task UpdateButtonAsync(DeckLayout layout, DeckPage page, DeckButtonConfig config);
    }
}
