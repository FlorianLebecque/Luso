#nullable enable

namespace Luso.Shared.Components.Deck
{
    /// <summary>
    /// Runtime registry of all known <see cref="IDeckButtonType"/> implementations.
    /// Populated at startup in <c>MauiProgram.cs</c>; consumed by <see cref="DeckPadView"/>.
    /// </summary>
    internal interface IDeckButtonRegistry
    {
        /// <summary>Adds a button type. Throws <see cref="InvalidOperationException"/> on duplicate TypeId.</summary>
        void Register(IDeckButtonType type);

        /// <summary>Returns the type for <paramref name="typeId"/>, or null if not found.</summary>
        IDeckButtonType? Get(string typeId);

        /// <summary>All registered types, in registration order.</summary>
        IReadOnlyList<IDeckButtonType> GetAll();
    }
}
