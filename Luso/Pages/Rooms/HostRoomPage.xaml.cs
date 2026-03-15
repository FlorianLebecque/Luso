#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Features.Rooms.Services;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Pages;

public partial class HostRoomPage : ContentPage
{
    // ── Injected services ─────────────────────────────────────────────────────

    private readonly IRoomSessionStore _session;
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IGuestRosterService _roster;

    // ── Fixed-button toggle state ─────────────────────────────────────────────

    private bool _micActive;

    // References kept so we can update their visuals on press/toggle.
    private Button? _btnStrobe;
    private Button? _btnMic;
    private Button? _btnAll;

    // Pastel colour per target button — shown on press, gray at rest.
    private readonly Dictionary<Button, Color> _targetPastelColors = new();

    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Color ColInactive = Color.FromArgb("#383838"); // DarkSurfaceRaised
    private static readonly Color ColStrobe = Color.FromArgb("#0078D4"); // BrandPrimary
    private static readonly Color ColMicOn = Color.FromArgb("#FFB900"); // SemanticWarning
    private static readonly Color ColAllOn = Color.FromArgb("#0078D4"); // BrandPrimary

    // Per-target colour: bilinear gradient across the pad grid.
    // Corners: top-left=coral, top-right=sky, bottom-left=gold, bottom-right=mint.
    private static Color PastelAt(int row, int col, int totalRows, int totalCols)
    {
        var tl = Color.FromArgb("#F28B82"); // coral
        var tr = Color.FromArgb("#74C2E1"); // sky
        var bl = Color.FromArgb("#E9C46A"); // gold
        var br = Color.FromArgb("#80C9A4"); // mint
        float u = totalCols > 1 ? col / (float)(totalCols - 1) : 0f;
        float v = totalRows > 1 ? row / (float)(totalRows - 1) : 0f;
        static float L(float a, float b, float t) => a + (b - a) * t;
        float r = L(L(tl.Red, tr.Red, u), L(bl.Red, br.Red, u), v);
        float g = L(L(tl.Green, tr.Green, u), L(bl.Green, br.Green, u), v);
        float b2 = L(L(tl.Blue, tr.Blue, u), L(bl.Blue, br.Blue, u), v);
        return new Color(r, g, b2);
    }

    // ─────────────────────────────────────────────────────────────────────────

    public HostRoomPage()
    {
        var sp = IPlatformApplication.Current!.Services;
        _session = sp.GetRequiredService<IRoomSessionStore>();
        _orchestrator = sp.GetRequiredService<ITaskOrchestrator>();
        _roster = sp.GetRequiredService<IGuestRosterService>();
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

        lblRoomName.Text = room.RoomName;

        _roster.WireRoom(room);
        _roster.GuestsChanged += OnRosterGuestsChanged;

        RoomNotifications.SetHostStatus(room.RoomName, _roster.Guests.Count);
        RefreshPadGrid();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _orchestrator.StopAll();
        _micActive = false;

        if (_session.Current is { } room)
        {
            _roster.GuestsChanged -= OnRosterGuestsChanged;
            _roster.UnwireRoom(room);
        }
        _roster.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Fixed button handlers
    // ═════════════════════════════════════════════════════════════════════════

    private void OnStrobePressed(object sender, EventArgs e)
    {
        if (_btnStrobe is not null) _btnStrobe.BackgroundColor = ColStrobe;
        _orchestrator.Start(new StrobeTask(TargetKind.Flashlight, 10));
        _orchestrator.Start(new StrobeTask(TargetKind.Screen, 10));
    }

    private void OnStrobeReleased(object sender, EventArgs e)
    {
        if (_btnStrobe is not null) _btnStrobe.BackgroundColor = ColInactive;
        _orchestrator.Stop(TargetKind.Flashlight);
        _orchestrator.Stop(TargetKind.Screen);
    }

    private void OnMicToggled(object sender, EventArgs e)
    {
        _micActive = !_micActive;

        if (_micActive)
        {
            _orchestrator.Start(new AudioTask(TargetKind.Flashlight));
            _orchestrator.Start(new AudioTask(TargetKind.Screen));
        }
        else
        {
            _orchestrator.Stop(TargetKind.Flashlight);
            _orchestrator.Stop(TargetKind.Screen);
        }

        if (_btnMic is not null)
            _btnMic.BackgroundColor = _micActive ? ColMicOn : ColInactive;
    }

    private void OnAllPressed(object sender, EventArgs e)
    {
        if (_session.Current is not { IsHost: true } room) return;
        if (_btnAll is not null) _btnAll.BackgroundColor = ColAllOn;
        _ = room.FlashAsync(FlashAction.On, TargetKind.Flashlight);
        _ = room.FlashAsync(FlashAction.On, TargetKind.Screen);
    }

    private void OnAllReleased(object sender, EventArgs e)
    {
        if (_session.Current is not { IsHost: true } room) return;
        if (_btnAll is not null) _btnAll.BackgroundColor = ColInactive;
        _ = room.FlashAsync(FlashAction.Off, TargetKind.Flashlight);
        _ = room.FlashAsync(FlashAction.Off, TargetKind.Screen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Per-target hold handlers
    // ═════════════════════════════════════════════════════════════════════════

    private void OnTargetPressed(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: (string deviceId, string targetId) } btn) return;
        if (_session.Current is not { IsHost: true } room) return;
        btn.BackgroundColor = _targetPastelColors.TryGetValue(btn, out var c) ? c : ColAllOn;
        _ = room.FlashTargetAsync(deviceId, FlashAction.On, targetId);
    }

    private void OnTargetReleased(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: (string deviceId, string targetId) } btn) return;
        if (_session.Current is not { IsHost: true } room) return;
        btn.BackgroundColor = ColInactive;
        _ = room.FlashTargetAsync(deviceId, FlashAction.Off, targetId);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Pad grid builder
    // ═════════════════════════════════════════════════════════════════════════

    private void RefreshPadGrid()
    {
        _btnMic = null;
        _btnAll = null;

        padGrid.RowDefinitions.Clear();
        padGrid.ColumnDefinitions.Clear();
        padGrid.Children.Clear();
        _targetPastelColors.Clear();

        const int cols = 3;

        // ── Collect per-target buttons ────────────────────────────────────────

        var room = _session.Current;
        var targetItems = new List<(string DeviceName, string TargetLabel,
                                   string DeviceId, string TargetId)>();

        void AddDevice(string deviceName, string deviceId, IReadOnlyList<ITarget> targets)
        {
            foreach (var t in targets)
            {
                if (t.Kind != TargetKind.Flashlight && t.Kind != TargetKind.Screen) continue;
                targetItems.Add((deviceName, t.DisplayName, deviceId, t.TargetId));
            }
        }

        if (room?.LocalDevice is { } local)
            // Host is the control center — show only Flashlight, never Screen.
            AddDevice(DeviceInfo.Current.Name + " (You)", local.DeviceId,
                      local.Targets.Where(t => t.Kind == TargetKind.Flashlight).ToList());

        foreach (var guest in _roster.Guests)
        {
            var device = room?.GetDevices().FirstOrDefault(d => d.DeviceId == guest.Ip);
            if (device is not null)
                AddDevice(guest.DisplayName, device.DeviceId, device.Targets);
        }

        // ── Build row definitions ─────────────────────────────────────────────

        int totalItems = 3 + targetItems.Count;
        int rows = (int)Math.Ceiling(totalItems / (double)cols);

        for (int r = 0; r < rows; r++)
            padGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (int c = 0; c < cols; c++)
            padGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        // ── Fixed row: Strobe / Mic / All ─────────────────────────────────────

        _btnStrobe = MakeFixedButton(
            "Strobe", ColInactive,
            pressed: OnStrobePressed, released: OnStrobeReleased,
            row: 0, col: 0);
        padGrid.Children.Add(_btnStrobe);

        _btnMic = MakeFixedButton(
            "Mic", _micActive ? ColMicOn : ColInactive,
            clicked: OnMicToggled,
            row: 0, col: 1);
        padGrid.Children.Add(_btnMic);

        _btnAll = MakeFixedButton(
            "All", ColInactive,
            pressed: OnAllPressed, released: OnAllReleased,
            row: 0, col: 2);
        padGrid.Children.Add(_btnAll);

        // ── Dynamic target buttons ────────────────────────────────────────────

        for (int i = 0; i < targetItems.Count; i++)
        {
            var (deviceName, targetLabel, deviceId, targetId) = targetItems[i];
            int slot = i + 3; // offset past fixed row
            int row = slot / cols;
            int col = slot % cols;

            var btn = new Button
            {
                Text = $"{deviceName}\n{targetLabel}",
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap,
                BackgroundColor = ColInactive,
                TextColor = Colors.White,
                CornerRadius = 14,
                CommandParameter = (deviceId, targetId),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            _targetPastelColors[btn] = PastelAt(row - 1, col, Math.Max(1, rows - 1), cols);
            btn.Pressed += OnTargetPressed;
            btn.Released += OnTargetReleased;

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            padGrid.Children.Add(btn);
        }
    }

    private static Button MakeFixedButton(
        string label, Color bg,
        EventHandler? clicked = null,
        EventHandler? pressed = null,
        EventHandler? released = null,
        int row = 0, int col = 0)
    {
        var btn = new Button
        {
            Text = label,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            BackgroundColor = bg,
            TextColor = Colors.White,
            CornerRadius = 14,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        if (clicked is not null) btn.Clicked += clicked;
        if (pressed is not null) btn.Pressed += pressed;
        if (released is not null) btn.Released += released;

        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col);
        return btn;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Roster change callback
    // ═════════════════════════════════════════════════════════════════════════

    private void OnRosterGuestsChanged(object? _, EventArgs __)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshPadGrid();
            RoomNotifications.SetHostStatus(
                _session.Current?.RoomName ?? string.Empty,
                _roster.Guests.Count);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Toolbar — Invite (＋)
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
    // Toolbar — Close room
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        RoomNotifications.Clear();
        await _session.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }
}