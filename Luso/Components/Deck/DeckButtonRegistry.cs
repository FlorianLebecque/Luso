#nullable enable

namespace Luso.Shared.Components.Deck
{
    /// <summary>
    /// Thread-safe dictionary-backed implementation of <see cref="IDeckButtonRegistry"/>.
    /// Registered as a singleton in DI.
    /// </summary>
    internal sealed class DeckButtonRegistry : IDeckButtonRegistry
    {
        private readonly Dictionary<string, IDeckButtonType> _map = new();
        private readonly List<IDeckButtonType> _ordered = new();

        public void Register(IDeckButtonType type)
        {
            if (_map.ContainsKey(type.TypeId))
                throw new InvalidOperationException(
                    $"DeckButtonRegistry: TypeId '{type.TypeId}' is already registered.");

            _map[type.TypeId] = type;
            _ordered.Add(type);
        }

        public IDeckButtonType? Get(string typeId) =>
            _map.TryGetValue(typeId, out var t) ? t : null;

        public IReadOnlyList<IDeckButtonType> GetAll() => _ordered;
    }
}
