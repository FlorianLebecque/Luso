#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Features.Rooms.Services;
using Luso.Shared.Components.Deck;
using Luso.Shared.Deck.Models;
using Luso.Shared.Deck.Registry;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Pages;

public partial class HostRoomPage : ContentPage
{
    // ── Injected services ─────────────────────────────────────────────────────

    private readonly IRoomSessionStore _session;
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IGuestRosterService _roster;
    private readonly IDeckButtonRegistry _deckRegistry;

    // ── Deck state (fully in-memory, never persisted) ─────────────────────────

    /// <summary>Index of the currently visible page; preserved across roster rebuilds.</summary>
    private int _activePageIndex;

    // Grid dimensions for auto-generated pages.
    private const int DeckCols = 3;

    // DeckRows is computed at runtime from screen metrics (see ComputeDeckRows).
    // Using a static fallback so BuildLayout always has a sane value even before
    // the first render pass.
    private int _deckRows = 3;

    private static readonly int PageHorizontalPadding = 32;  // 16 + 16
    private static readonly int CellGap = 8;
    private static readonly int TabBarHeight = 52;  // DeckPageTabBar
    private static readonly int BottomBarHeight = 68;  // BottomBarView
    private static readonly int VerticalPadding = 8;   // top padding on page grid
    private static readonly int RowSpacingBetween = 8;   // RowSpacing in page Grid

    /// <summary>
    /// Computes how many 1:1 square rows fit in the available deck area, using the
    /// same arithmetic as <c>DeckPadView.UpdateRowHeights()</c>.
    /// </summary>
    private void RefreshDeckRows()
    {
        var info = DeviceDisplay.Current.MainDisplayInfo;
        double density = info.Density > 0 ? info.Density : 1.0;
        double dpW = info.Width / density;
        double dpH = info.Height / density;

        double gridW = dpW - PageHorizontalPadding;
        double cellSize = (gridW - CellGap * (DeckCols - 1)) / DeckCols;
        if (cellSize <= 0) return;

        // Available height for the DeckPadView (row 1 = Star in the 3-row page layout).
        double available = dpH
            - VerticalPadding
            - TabBarHeight - RowSpacingBetween
            - BottomBarHeight;

        int rows = (int)Math.Floor((available + CellGap) / (cellSize + CellGap));
        _deckRows = Math.Max(2, rows);  // at least 2 rows
    }

    // ─────────────────────────────────────────────────────────────────────────

    public HostRoomPage()
    {
        var sp = IPlatformApplication.Current!.Services;
        _session = sp.GetRequiredService<IRoomSessionStore>();
        _orchestrator = sp.GetRequiredService<ITaskOrchestrator>();
        _roster = sp.GetRequiredService<IGuestRosterService>();
        _deckRegistry = sp.GetRequiredService<IDeckButtonRegistry>();
        InitializeComponent();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Page lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var room = _session.Current;
        if (room is null || !room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        _roster.WireRoom(room);
        _roster.GuestsChanged += OnRosterGuestsChanged;

        RoomNotifications.SetHostStatus(room.RoomName, _roster.Guests.Count);

        deckPad.Registry = _deckRegistry;
        _activePageIndex = 0;
        RefreshDeckRows();
        BindDeck(room);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _orchestrator.StopAll();

        if (_session.Current is { } room)
        {
            _roster.GuestsChanged -= OnRosterGuestsChanged;
            _roster.UnwireRoom(room);
        }
        _roster.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Deck wiring — fully dynamic, never persisted
    // ═════════════════════════════════════════════════════════════════════════

    private void BindDeck(Room room)
    {
        var layout = BuildLayout(room);

        // Clamp active index in case page count shrank.
        _activePageIndex = Math.Clamp(_activePageIndex, 0, layout.Pages.Count - 1);
        var activePage = layout.Pages[_activePageIndex];

        deckTabBar.DeckLayout = layout;
        deckTabBar.ActivePage = activePage;

        var ctx = new DeckButtonContext { Room = room, Orchestrator = _orchestrator };
        deckPad.Update(activePage, ctx);
        deckPad.ExtraButtons = null; // targets are now first-class deck buttons
    }

    /// <summary>
    /// Builds a fully in-memory <see cref="DeckLayout"/> by collecting all buttons
    /// (fixed controls + every connected-device target) and packing them into
    /// pages of <see cref="DeckRows"/> × <see cref="DeckCols"/> automatically.
    /// </summary>
    private DeckLayout BuildLayout(Room room)
    {
        // 1. Ordered flat list of all button configs (no row/col yet).
        var all = new List<DeckButtonConfig>
        {
            new() { TypeId = "strobe" },
            new() { TypeId = "mic" },
            new() { TypeId = "all.flash" },
        };

        void AddDevice(string deviceName, string deviceId, IReadOnlyList<ITarget> targets)
        {
            foreach (var t in targets)
            {
                if (t.Kind != TargetKind.Flashlight && t.Kind != TargetKind.Screen) continue;
                all.Add(new DeckButtonConfig
                {
                    TypeId = "target.flash",
                    Label = $"{deviceName}\n{t.DisplayName}",
                    Params = new Dictionary<string, string>
                    {
                        ["deviceId"] = deviceId,
                        ["targetId"] = t.TargetId,
                    },
                });
            }
        }

        if (room.LocalDevice is { } local)
            AddDevice(DeviceInfo.Current.Name + " (You)", local.DeviceId,
                local.Targets.Where(t => t.Kind == TargetKind.Flashlight).ToList());

        foreach (var guest in _roster.Guests)
        {
            var device = room.GetDevices().FirstOrDefault(d => d.DeviceId == guest.Ip);
            if (device is not null)
                AddDevice(guest.DisplayName, device.DeviceId, device.Targets);
        }

        // 2. Pack into pages of _deckRows × DeckCols.
        int slotsPerPage = _deckRows * DeckCols;
        var layout = new DeckLayout { LayoutId = "host-runtime" };
        int pageIndex = 0;

        for (int start = 0; start < all.Count; start += slotsPerPage, pageIndex++)
        {
            var page = new DeckPage
            {
                Name = pageIndex == 0 ? room.RoomName : $"Page {pageIndex + 1}",
                Rows = _deckRows,
                Cols = DeckCols,
            };

            for (int s = 0; s < slotsPerPage && start + s < all.Count; s++)
                page.Buttons.Add(all[start + s].WithSlot(s / DeckCols, s % DeckCols));

            layout.Pages.Add(page);
        }

        if (layout.Pages.Count == 0)
            layout.Pages.Add(new DeckPage { Name = room.RoomName, Rows = _deckRows, Cols = DeckCols });

        return layout;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Tab bar events
    // ═════════════════════════════════════════════════════════════════════════

    private void OnPageSelected(object sender, DeckPage page)
    {
        // The pages in the tab bar belong to the current layout; find index by id.
        if (deckTabBar.DeckLayout is not { } layout) return;
        var idx = layout.Pages.FindIndex(p => p.PageId == page.PageId);
        if (idx >= 0) _activePageIndex = idx;

        deckTabBar.ActivePage = page;
        deckPad.Page = page;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Roster change callback
    // ═════════════════════════════════════════════════════════════════════════

    private void OnRosterGuestsChanged(object? _, EventArgs __)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_session.Current is { } room)
                BindDeck(room); // full rebuild — targets are now deck buttons

            RoomNotifications.SetHostStatus(
                _session.Current?.RoomName ?? string.Empty,
                _roster.Guests.Count);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Toolbar — Invite
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnInviteToolbarClicked(object sender, EventArgs e)
    {
        if (_roster.Candidates.Count == 0)
        {
            await DisplayAlert("Invite", "No candidates discovered yet.\nMake sure other devices have the app open.", "OK");
            return;
        }

        string[] names = _roster.Candidates.Select(c => $"{c.DeviceName}  ({c.Address})").ToArray();
        string? choice = await DisplayActionSheet("Invite a guest", "Cancel", null, names);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var candidate = _roster.Candidates.FirstOrDefault(c => $"{c.DeviceName}  ({c.Address})" == choice);
        if (candidate is not null)
            await SendInviteAsync(candidate);
    }

    private async Task SendInviteAsync(IDiscoveredDevice candidate)
    {
        if (_session.Current is not { IsHost: true } room) return;

        if (candidate.PairingHint is { } hint)
        {
            bool confirmed = await DisplayAlert(
                "Pairing required", hint, "OK — I pressed the button", "Cancel");
            if (!confirmed) return;
        }

        try
        {
            await room.SendInviteAsync(candidate);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Invite failed", ex.Message, "OK");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Toolbar — Kick
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnKickToolbarClicked(object sender, EventArgs e)
    {
        if (_roster.Guests.Count == 0)
        {
            await DisplayAlert("Kick", "No guests are currently connected.", "OK");
            return;
        }

        string[] names = _roster.Guests.Select(g => g.DisplayName).ToArray();
        string? choice = await DisplayActionSheet("Kick a guest", "Cancel", null, names);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var guest = _roster.Guests.FirstOrDefault(g => g.DisplayName == choice);
        if (guest is not null)
            await KickGuestAsync(guest);
    }

    private async Task KickGuestAsync(GuestInfo guest)
    {
        if (_session.Current is not { IsHost: true } room) return;

        try
        {
            bool kicked = await room.KickDeviceAsync(guest.Ip);
            if (!kicked) { await DisplayAlert("Kick failed", "Guest is no longer connected.", "OK"); return; }
            _roster.RemoveGuest(guest.Ip);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Kick failed", ex.Message, "OK");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Back-button interception
    // ═════════════════════════════════════════════════════════════════════════

    protected override bool OnBackButtonPressed()
    {
        // Fire-and-forget: intercept the press and ask asynchronously.
        _ = ConfirmLeaveAsync();
        return true;
    }

    private async Task ConfirmLeaveAsync()
    {
        bool confirmed = await DisplayAlert(
            "Close room?",
            "Leaving will disconnect all guests and close the room.",
            "Close room", "Stay");
        if (confirmed)
            await LeaveRoomAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Toolbar — Close room
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnLeaveClicked(object sender, EventArgs e) => await LeaveRoomAsync();

    private async Task LeaveRoomAsync()
    {
        RoomNotifications.Clear();
        await _session.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }
}
