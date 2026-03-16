#nullable enable
using Luso.Shared.Deck.Models;

namespace Luso.Shared.Components.Deck
{
    /// <summary>
    /// Horizontal tab strip for switching between <see cref="DeckPage"/> instances.
    /// Fires <see cref="PageSelected"/> when a tab is tapped and
    /// <see cref="AddPageRequested"/> when the "+" button is tapped.
    /// </summary>
    public partial class DeckPageTabBar : ContentView
    {
        // ── Events ─────────────────────────────────────────────────────────────

        public event EventHandler<DeckPage>? PageSelected;
        public event EventHandler? AddPageRequested;

        // ── Bindable properties ────────────────────────────────────────────────

        public static readonly BindableProperty LayoutProperty =
            BindableProperty.Create("DeckLayout", typeof(DeckLayout), typeof(DeckPageTabBar),
                null, propertyChanged: (b, _, _) => ((DeckPageTabBar)b).RebuildTabs());

        public static readonly BindableProperty ActivePageProperty =
            BindableProperty.Create(nameof(ActivePage), typeof(DeckPage), typeof(DeckPageTabBar),
                null, propertyChanged: (b, _, _) => ((DeckPageTabBar)b).RefreshActiveState());

        public static readonly BindableProperty AllowAddPageProperty =
            BindableProperty.Create(nameof(AllowAddPage), typeof(bool), typeof(DeckPageTabBar),
                true, propertyChanged: (b, _, n) => ((DeckPageTabBar)b).btnAdd.IsVisible = (bool)n);

        public DeckLayout? DeckLayout
        {
            get => (DeckLayout?)GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        public DeckPage? ActivePage
        {
            get => (DeckPage?)GetValue(ActivePageProperty);
            set => SetValue(ActivePageProperty, value);
        }

        /// <summary>When false the "+" add-page button is hidden. Default true.</summary>
        public bool AllowAddPage
        {
            get => (bool)GetValue(AllowAddPageProperty);
            set => SetValue(AllowAddPageProperty, value);
        }

        // ── Colors ─────────────────────────────────────────────────────────────

        private static readonly Color ColTabActive = Color.FromArgb("#0078D4");
        private static readonly Color ColTabInactive = Color.FromArgb("#2A2A2A");
        private static readonly Color ColTabTextActive = Colors.White;
        private static readonly Color ColTabTextInactive = Color.FromArgb("#AAAAAA");

        // ── Construction ───────────────────────────────────────────────────────

        public DeckPageTabBar()
        {
            InitializeComponent();
        }

        // ── Tab building ───────────────────────────────────────────────────────

        private void RebuildTabs()
        {
            tabHost.Children.Clear();

            if (DeckLayout is null) return;

            foreach (var page in DeckLayout.Pages)
                tabHost.Children.Add(BuildTab(page));

            RefreshActiveState();
        }

        private Button BuildTab(DeckPage page)
        {
            var btn = new Button
            {
                Text = page.Name,
                FontSize = 13,
                CornerRadius = 10,
                Padding = new Thickness(12, 0),
                HeightRequest = 36,
                BackgroundColor = ColTabInactive,
                TextColor = ColTabTextInactive,
            };
            btn.Clicked += (_, _) => PageSelected?.Invoke(this, page);
            return btn;
        }

        private void RefreshActiveState()
        {
            if (DeckLayout is null) return;

            var pages = DeckLayout.Pages;
            for (int i = 0; i < tabHost.Children.Count && i < pages.Count; i++)
            {
                if (tabHost.Children[i] is not Button btn) continue;
                bool isActive = pages[i].PageId == ActivePage?.PageId;
                btn.BackgroundColor = isActive ? ColTabActive : ColTabInactive;
                btn.TextColor = isActive ? ColTabTextActive : ColTabTextInactive;
            }
        }

        private void OnAddClicked(object sender, EventArgs e) =>
            AddPageRequested?.Invoke(this, EventArgs.Empty);
    }
}
