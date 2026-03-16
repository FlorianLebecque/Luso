#nullable enable
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;
using Microsoft.Maui.Controls.Shapes;

namespace Luso.Shared.Components.Deck
{
    /// <summary>
    /// Renders a <see cref="DeckPage"/> as an interactive button grid.
    ///
    /// Automatically rebuilds when <see cref="Page"/>, <see cref="Context"/>, or
    /// <see cref="ExtraButtons"/> change. <see cref="ExtraButtons"/> are runtime-only
    /// (e.g. connected-guest targets) and are appended after the page's own buttons
    /// without being persisted.
    /// </summary>
    public partial class DeckPadView : ContentView
    {
        private bool _sizeHandlerAttached;

        // ── Bindable properties ───────────────────────────────────────────────

        public static readonly BindableProperty PageProperty =
            BindableProperty.Create(nameof(Page), typeof(DeckPage), typeof(DeckPadView),
                null, propertyChanged: (b, _, _) => ((DeckPadView)b).Rebuild());

        public static readonly BindableProperty ContextProperty =
            BindableProperty.Create(nameof(Context), typeof(DeckButtonContext), typeof(DeckPadView),
                null, propertyChanged: (b, _, _) => ((DeckPadView)b).Rebuild());

        public static readonly BindableProperty ExtraButtonsProperty =
            BindableProperty.Create(nameof(ExtraButtons), typeof(IReadOnlyList<DeckButtonConfig>),
                typeof(DeckPadView), null, propertyChanged: (b, _, _) => ((DeckPadView)b).Rebuild());

        public static readonly BindableProperty RegistryProperty =
            BindableProperty.Create(nameof(Registry), typeof(IDeckButtonRegistry), typeof(DeckPadView),
                null, propertyChanged: (b, _, _) => ((DeckPadView)b).Rebuild());

        public DeckPage? Page
        {
            get => (DeckPage?)GetValue(PageProperty);
            set => SetValue(PageProperty, value);
        }

        internal DeckButtonContext? Context
        {
            get => (DeckButtonContext?)GetValue(ContextProperty);
            set => SetValue(ContextProperty, value);
        }

        public IReadOnlyList<DeckButtonConfig>? ExtraButtons
        {
            get => (IReadOnlyList<DeckButtonConfig>?)GetValue(ExtraButtonsProperty);
            set => SetValue(ExtraButtonsProperty, value);
        }

        internal IDeckButtonRegistry? Registry
        {
            get => (IDeckButtonRegistry?)GetValue(RegistryProperty);
            set => SetValue(RegistryProperty, value);
        }

        // ── Construction ──────────────────────────────────────────────────────

        public DeckPadView()
        {
            InitializeComponent();
        }

        // ── Square-cell sizing ────────────────────────────────────────────────

        /// <summary>
        /// Called whenever the grid's rendered size changes. Sets every row height so
        /// cells are square, then extends the grid with placeholder rows to fill any
        /// remaining vertical space.
        /// </summary>
        private void OnInnerGridSizeChanged(object? sender, EventArgs e) => UpdateRowHeights();

        private void UpdateRowHeights()
        {
            int cols = innerGrid.ColumnDefinitions.Count;
            if (cols == 0 || innerGrid.Width <= 0 || innerGrid.Height <= 0) return;

            double colGap = innerGrid.ColumnSpacing * (cols - 1);
            double cellSize = (innerGrid.Width - colGap) / cols;
            if (cellSize <= 0) return;

            // How many square rows fit in the available height?
            double rowGap = innerGrid.RowSpacing;
            int fitsRows = (int)Math.Floor((innerGrid.Height + rowGap) / (cellSize + rowGap));
            fitsRows = Math.Max(fitsRows, innerGrid.RowDefinitions.Count);

            // Extend the grid with placeholder rows to fill all available vertical space.
            int currentRows = innerGrid.RowDefinitions.Count;
            for (int r = currentRows; r < fitsRows; r++)
            {
                innerGrid.RowDefinitions.Add(new RowDefinition(new GridLength(cellSize)));
                for (int c = 0; c < cols; c++)
                {
                    var ph = BuildEmptyCell();
                    Grid.SetRow(ph, r);
                    Grid.SetColumn(ph, c);
                    innerGrid.Children.Add(ph);
                }
            }

            // Apply square height to every row.
            foreach (var rd in innerGrid.RowDefinitions)
                rd.Height = new GridLength(cellSize);
        }

        // ── Grid rebuild ──────────────────────────────────────────────────────

        private void Rebuild()
        {
            innerGrid.RowDefinitions.Clear();
            innerGrid.ColumnDefinitions.Clear();
            innerGrid.Children.Clear();

            // Attach size handler once so row heights track grid width changes.
            if (!_sizeHandlerAttached)
            {
                innerGrid.SizeChanged += OnInnerGridSizeChanged;
                _sizeHandlerAttached = true;
            }

            if (Page is null || Registry is null) return;

            var allButtons = new List<DeckButtonConfig>(Page.Buttons);
            if (ExtraButtons is not null) allButtons.AddRange(ExtraButtons);

            // Determine effective grid size — must be at least big enough for all buttons.
            int rows = Page.Rows;
            int cols = Page.Cols;

            foreach (var btn in allButtons)
            {
                rows = Math.Max(rows, btn.Row + btn.RowSpan);
                cols = Math.Max(cols, btn.Col + btn.ColSpan);
            }

            // Use Star rows initially; UpdateRowHeights() will convert to absolute once laid out.
            for (int r = 0; r < rows; r++)
                innerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            for (int c = 0; c < cols; c++)
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            // Track every (row, col) unit that is covered by a placed button (including spans).
            var occupied = new HashSet<(int r, int c)>();
            foreach (var btn in allButtons)
                for (int r = btn.Row; r < btn.Row + btn.RowSpan; r++)
                    for (int c = btn.Col; c < btn.Col + btn.ColSpan; c++)
                        occupied.Add((r, c));

            // ── Empty-cell placeholders ─────────────────────────────────────
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (occupied.Contains((r, c))) continue;
                    var ph = BuildEmptyCell();
                    Grid.SetRow(ph, r);
                    Grid.SetColumn(ph, c);
                    innerGrid.Children.Add(ph);
                }
            }

            // ── Active buttons ──────────────────────────────────────────────
            int extraRowStart = Page.Rows;

            foreach (var cfg in allButtons)
            {
                var type = Registry.Get(cfg.TypeId);
                if (type is null) continue;

                Color? accent = cfg.Row >= extraRowStart
                    ? PastelAt(cfg.Row - extraRowStart, cfg.Col,
                               Math.Max(1, rows - extraRowStart), cols)
                    : null;

                var cellCtx = (Context ?? new DeckButtonContext()) with { AccentColor = accent };

                var view = type.BuildView(cfg, cellCtx);
                Grid.SetRow(view, cfg.Row);
                Grid.SetColumn(view, cfg.Col);
                if (cfg.RowSpan > 1) Grid.SetRowSpan(view, cfg.RowSpan);
                if (cfg.ColSpan > 1) Grid.SetColumnSpan(view, cfg.ColSpan);

                innerGrid.Children.Add(view);
            }

            // Trigger square sizing immediately if width is already known.
            UpdateRowHeights();
        }

        private static View BuildEmptyCell() => new Border
        {
            StrokeThickness = 2,
            Stroke = new SolidColorBrush(Color.FromArgb("#3A3A3A")),
            StrokeDashArray = new DoubleCollection { 5, 4 },
            BackgroundColor = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        // ── Gradient helper ───────────────────────────────────────────────────

        /// <summary>
        /// Bilinear gradient across the extra-button region.
        /// Corners: TL=coral, TR=sky, BL=gold, BR=mint.
        /// </summary>
        private static Color PastelAt(int row, int col, int totalRows, int totalCols)
        {
            var tl = Color.FromArgb("#F28B82");
            var tr = Color.FromArgb("#74C2E1");
            var bl = Color.FromArgb("#E9C46A");
            var br = Color.FromArgb("#80C9A4");
            float u = totalCols > 1 ? col / (float)(totalCols - 1) : 0f;
            float v = totalRows > 1 ? row / (float)(totalRows - 1) : 0f;
            static float L(float a, float b, float t) => a + (b - a) * t;
            float r = L(L(tl.Red, tr.Red, u), L(bl.Red, br.Red, u), v);
            float g = L(L(tl.Green, tr.Green, u), L(bl.Green, br.Green, u), v);
            float b2 = L(L(tl.Blue, tr.Blue, u), L(bl.Blue, br.Blue, u), v);
            return new Color(r, g, b2);
        }
    }
}
