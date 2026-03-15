#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Features.Rooms.Services;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Pages;

public partial class HostRoomPage : ContentPage
{
    // ── Mode ─────────────────────────────────────────────────────────────────

    private enum FlashMode { Off, On, Auto }
    private FlashMode _mode = FlashMode.Off;

    // ── Injected services ─────────────────────────────────────────────────────

    private readonly IRoomSessionStore _session;
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IGuestRosterService _roster;

    // ── Manual pad task (active in Off mode) ──────────────────────────────────
    private ManualTask? _manualTask;

    // ── Pad ───────────────────────────────────────────────────────────────────

    // Pastel palette — readable on the dark (#1C1C1C) background
    private static readonly Color[] PadColors = {
        Color.FromArgb("#F28B82"), // coral
        Color.FromArgb("#F4A261"), // peach
        Color.FromArgb("#E9C46A"), // gold
        Color.FromArgb("#80C9A4"), // mint
        Color.FromArgb("#74C2E1"), // sky blue
        Color.FromArgb("#8AB4F8"), // periwinkle
        Color.FromArgb("#C58AF9"), // lavender
        Color.FromArgb("#F48FB1"), // pink
        Color.FromArgb("#7DCFBF"), // teal
        Color.FromArgb("#F4B183"), // melon
        Color.FromArgb("#A8D8A8"), // sage green
        Color.FromArgb("#E0C89A"), // wheat
        Color.FromArgb("#90C8E8"), // powder blue
        Color.FromArgb("#D4A0A8"), // dusty rose
        Color.FromArgb("#A8D0B8"), // mint sage
        Color.FromArgb("#A0B8D8"), // slate blue
    };

    // ─────────────────────────────────────────────────────────────────────────

    public HostRoomPage()
    {
        var sp = IPlatformApplication.Current!.Services;
        _session = sp.GetRequiredService<IRoomSessionStore>();
        _orchestrator = sp.GetRequiredService<ITaskOrchestrator>();
        _roster = sp.GetRequiredService<IGuestRosterService>();
        InitializeComponent();
        UpdateModeButtons();
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
        ApplyMode();
        RefreshPadGrid();
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
    // Mode selector
    // ═════════════════════════════════════════════════════════════════════════

    private void OnModeClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        _mode = btn.Text switch
        {
            "On" => FlashMode.On,
            "Auto" => FlashMode.Auto,
            _ => FlashMode.Off,
        };

        UpdateModeButtons();
        ApplyMode();
    }

    private void UpdateModeButtons()
    {
        // Determine target segment (0=Off, 1=On, 2=Auto)
        int segment = _mode switch
        {
            FlashMode.Off => 0,
            FlashMode.On => 1,
            _ => 2,
        };

        // Pill width = inner container width / 3 (container has 4px padding each side)
        double innerW = modeContainer.Width > 8 ? modeContainer.Width - 8 : 0;
        double segW = innerW / 3.0;
        _ = modePill.TranslateTo(segment * segW, 0, 220, Easing.CubicOut);

        // Update label colours
        var active = Colors.White;
        var inactive = Color.FromArgb("#B3B0AD");
        lblModeOff.TextColor = _mode == FlashMode.Off ? active : inactive;
        lblModeOn.TextColor = _mode == FlashMode.On ? active : inactive;
        lblModeAuto.TextColor = _mode == FlashMode.Auto ? active : inactive;

        sliderPanel.IsVisible = _mode == FlashMode.On;
    }

    private void ApplyMode()
    {
        switch (_mode)
        {
            case FlashMode.On:
                _manualTask = null;
                _orchestrator.Start(new StrobeTask(Domain.Targets.TargetKind.Flashlight, frequencySlider.Value));
                break;
            case FlashMode.Auto:
                _manualTask = null;
                _orchestrator.Start(new AudioTask(Domain.Targets.TargetKind.Flashlight));
                break;
            default:
                _manualTask = new ManualTask(Domain.Targets.TargetKind.Flashlight);
                _orchestrator.Start(_manualTask);
                break;
        }
    }

    private void OnFrequencyChanged(object sender, ValueChangedEventArgs e)
    {
        lblFrequency.Text = $"Frequency: {e.NewValue:F1} Hz";
        if (_mode == FlashMode.On)
            _orchestrator.Start(new StrobeTask(Domain.Targets.TargetKind.Flashlight, e.NewValue));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Pad — press = light ON everywhere, release = light OFF everywhere
    // ═════════════════════════════════════════════════════════════════════════

    private void OnPadPressed(object sender, EventArgs e)
    {
        if (_manualTask is null) return;
        if (sender is not Button { CommandParameter: var param }) return;
        var deviceId = param as string ?? _session.Current?.LocalDevice?.DeviceId;
        _manualTask.Fire(FlashAction.On, deviceId);
    }

    private void OnPadReleased(object sender, EventArgs e)
    {
        if (_manualTask is null) return;
        if (sender is not Button { CommandParameter: var param }) return;
        var deviceId = param as string ?? _session.Current?.LocalDevice?.DeviceId;
        _manualTask.Fire(FlashAction.Off, deviceId);
    }

    private void RefreshPadGrid()
    {
        // Build the ordered device list: host always first
        var items = new List<(string Label, string? Ip)>
        {
            (DeviceInfo.Current.Name + "\n(You)", null),
        };
        foreach (var g in _roster.Guests)
            items.Add((g.DisplayName, g.Ip));

        int n = items.Count;

        // Smart column count: favour square-ish grids
        int cols = n switch
        {
            1 => 1,
            <= 4 => 2,
            <= 9 => 3,
            _ => 4,
        };

        int rows = (int)Math.Ceiling(n / (double)cols);
        int totalSlots = rows * cols;
        int leftover = totalSlots - n;

        // Leftover empty slots → absorbed into the first (host) pad as extra column-span,
        // making it the "hero" tile and avoiding wasted whitespace.
        int firstColSpan = 1 + leftover;

        padGrid.RowDefinitions.Clear();
        padGrid.ColumnDefinitions.Clear();
        padGrid.Children.Clear();

        for (int r = 0; r < rows; r++)
            padGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (int c = 0; c < cols; c++)
            padGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        int gridCol = 0;
        int gridRow = 0;

        for (int i = 0; i < n; i++)
        {
            var (label, ip) = items[i];
            int span = (i == 0) ? firstColSpan : 1;

            var btn = new Button
            {
                Text = label,
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap,
                BackgroundColor = PadColors[i % PadColors.Length],
                TextColor = Colors.Black,
                CornerRadius = 14,
                CommandParameter = ip,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };
            btn.Pressed += OnPadPressed;
            btn.Released += OnPadReleased;

            Grid.SetRow(btn, gridRow);
            Grid.SetColumn(btn, gridCol);
            Grid.SetColumnSpan(btn, span);
            padGrid.Children.Add(btn);

            // Advance cursor, wrapping to next row when the current one is full
            gridCol += span;
            if (gridCol >= cols)
            {
                gridCol = 0;
                gridRow++;
            }
        }
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
