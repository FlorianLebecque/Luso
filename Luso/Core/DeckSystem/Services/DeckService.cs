#nullable enable
using System.Text.Json;
using Luso.Shared.Deck.Models;

namespace Luso.Shared.Deck.Services
{
    /// <summary>
    /// Persists <see cref="DeckLayout"/> to <c>AppDataDirectory/deck-{layoutId}.json</c>.
    /// All operations are thread-safe via a per-layout semaphore.
    /// </summary>
    internal sealed class DeckService : IDeckService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly Dictionary<string, SemaphoreSlim> _locks = new();
        private readonly Dictionary<string, DeckLayout> _cache = new();

        // ── IDeckService ──────────────────────────────────────────────────────

        public async Task<DeckLayout> GetLayoutAsync(string layoutId = "default")
        {
            await LockFor(layoutId).WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(layoutId, out var cached))
                    return cached;

                var layout = await LoadFromDiskAsync(layoutId).ConfigureAwait(false)
                             ?? CreateDefault(layoutId);
                _cache[layoutId] = layout;
                return layout;
            }
            finally { LockFor(layoutId).Release(); }
        }

        public async Task SaveAsync(DeckLayout layout)
        {
            await LockFor(layout.LayoutId).WaitAsync().ConfigureAwait(false);
            try
            {
                _cache[layout.LayoutId] = layout;
                await WriteToDiskAsync(layout).ConfigureAwait(false);
            }
            finally { LockFor(layout.LayoutId).Release(); }
        }

        public async Task<DeckPage> AddPageAsync(DeckLayout layout, string name, int rows = 2, int cols = 3)
        {
            var page = new DeckPage { Name = name, Rows = rows, Cols = cols };
            layout.Pages.Add(page);
            await SaveAsync(layout).ConfigureAwait(false);
            return page;
        }

        public async Task RemovePageAsync(DeckLayout layout, string pageId)
        {
            layout.Pages.RemoveAll(p => p.PageId == pageId);
            await SaveAsync(layout).ConfigureAwait(false);
        }

        public async Task AddButtonAsync(DeckLayout layout, DeckPage page, DeckButtonConfig config)
        {
            page.Buttons.Add(config);
            await SaveAsync(layout).ConfigureAwait(false);
        }

        public async Task RemoveButtonAsync(DeckLayout layout, DeckPage page, string buttonId)
        {
            page.Buttons.RemoveAll(b => b.ButtonId == buttonId);
            await SaveAsync(layout).ConfigureAwait(false);
        }

        public async Task UpdateButtonAsync(DeckLayout layout, DeckPage page, DeckButtonConfig config)
        {
            var idx = page.Buttons.FindIndex(b => b.ButtonId == config.ButtonId);
            if (idx >= 0) page.Buttons[idx] = config;
            await SaveAsync(layout).ConfigureAwait(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FilePath(string layoutId) =>
            Path.Combine(FileSystem.AppDataDirectory, $"deck-{layoutId}.json");

        private static async Task<DeckLayout?> LoadFromDiskAsync(string layoutId)
        {
            var path = FilePath(layoutId);
            if (!File.Exists(path)) return null;

            try
            {
                await using var fs = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<DeckLayout>(fs, JsonOpts)
                       .ConfigureAwait(false);
            }
            catch
            {
                return null; // corrupt file → fall back to default
            }
        }

        private static async Task WriteToDiskAsync(DeckLayout layout)
        {
            var path = FilePath(layout.LayoutId);
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, layout, JsonOpts).ConfigureAwait(false);
        }

        private SemaphoreSlim LockFor(string layoutId)
        {
            if (!_locks.TryGetValue(layoutId, out var sem))
                _locks[layoutId] = sem = new SemaphoreSlim(1, 1);
            return sem;
        }

        /// <summary>Builds the built-in default layout (Strobe / Mic / AllFlash on one page).</summary>
        internal static DeckLayout CreateDefault(string layoutId)
        {
            var page = new DeckPage { Name = "Main", Rows = 2, Cols = 3 };

            page.Buttons.AddRange(new[]
            {
                new DeckButtonConfig { TypeId = "strobe",    Row = 0, Col = 0 },
                new DeckButtonConfig { TypeId = "mic",       Row = 0, Col = 1 },
                new DeckButtonConfig { TypeId = "all.flash", Row = 0, Col = 2 },
            });

            return new DeckLayout { LayoutId = layoutId, Pages = [page] };
        }
    }
}
