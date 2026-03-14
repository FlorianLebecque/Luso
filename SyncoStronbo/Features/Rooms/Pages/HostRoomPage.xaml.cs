#nullable enable
using System.Collections.ObjectModel;
using SyncoStronbo.Audio;
using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Features.Rooms.Networking;
using SyncoStronbo.Features.Rooms.Services;
using SyncoStronbo.Light;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

public partial class HostRoomPage : ContentPage
{
    // ── Mode ─────────────────────────────────────────────────────────────────

    private enum FlashMode { Off, On, Auto }
    private FlashMode _mode = FlashMode.Off;

    // ── Guest / candidate state ───────────────────────────────────────────────

    private readonly ObservableCollection<GuestInfo> _guestInfos = new();
    private readonly ObservableCollection<GuestPresenceAnnouncement> _candidates = new();
    private UdpRoomDiscovery? _inviteDiscovery;

    // ── Audio (auto mode) ─────────────────────────────────────────────────────

    private IAudioAnalyser? _audioAnalyser;
    private CancellationTokenSource? _bgCts;

    // ── Light ─────────────────────────────────────────────────────────────────

    private readonly LightController _light = LightController.GetInstance();

    // ── Pad ───────────────────────────────────────────────────────────────────

    private static readonly Color[] PadColors = {
        Color.FromArgb("#E74C3C"), Color.FromArgb("#E67E22"),
        Color.FromArgb("#F1C40F"), Color.FromArgb("#2ECC71"),
        Color.FromArgb("#1ABC9C"), Color.FromArgb("#3498DB"),
        Color.FromArgb("#9B59B6"), Color.FromArgb("#E91E63"),
        Color.FromArgb("#00BCD4"), Color.FromArgb("#FF5722"),
        Color.FromArgb("#8BC34A"), Color.FromArgb("#FF9800"),
        Color.FromArgb("#607D8B"), Color.FromArgb("#795548"),
        Color.FromArgb("#9E9E9E"), Color.FromArgb("#3F51B5"),
    };

    // ─────────────────────────────────────────────────────────────────────────

    public HostRoomPage()
    {
        InitializeComponent();
        UpdateModeButtons();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Page lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var room = RoomSession.Current;
        if (room is null || !room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text = room.RoomName;

        room.OnGuestConnected    += OnGuestConnected;
        room.OnGuestDisconnected += OnGuestDisconnected;
        room.OnGuestPingUpdated  += OnGuestPingUpdated;

        _inviteDiscovery = new UdpRoomDiscovery();
        _inviteDiscovery.OnGuestPresenceDiscovered += OnGuestPresenceDiscovered;
        _inviteDiscovery.OnInviteRefused           += OnInviteRefused;
        _inviteDiscovery.StartListening();

        foreach (var (ip, name, rtt) in room.GetGuests())
        {
            var info = GetOrAdd(ip, name);
            info.RttMs = rtt;
        }

        RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
        ApplyMode();
        RefreshPadGrid();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        StopBackground();
        _light.Stop();

        if (RoomSession.Current is { } room)
        {
            room.OnGuestConnected    -= OnGuestConnected;
            room.OnGuestDisconnected -= OnGuestDisconnected;
            room.OnGuestPingUpdated  -= OnGuestPingUpdated;
        }

        if (_inviteDiscovery is not null)
        {
            _inviteDiscovery.OnGuestPresenceDiscovered -= OnGuestPresenceDiscovered;
            _inviteDiscovery.OnInviteRefused           -= OnInviteRefused;
            _inviteDiscovery.Dispose();
            _inviteDiscovery = null;
        }

        _guestInfos.Clear();
        _candidates.Clear();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Mode selector
    // ═════════════════════════════════════════════════════════════════════════

    private void OnModeClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        _mode = btn.Text switch
        {
            "On"   => FlashMode.On,
            "Auto" => FlashMode.Auto,
            _      => FlashMode.Off,
        };

        UpdateModeButtons();
        ApplyMode();
    }

    private void UpdateModeButtons()
    {
        const string active   = "#2E86DE";
        const string inactive = "#444";

        btnModeOff.BackgroundColor  = Color.FromArgb(_mode == FlashMode.Off  ? active : inactive);
        btnModeOn.BackgroundColor   = Color.FromArgb(_mode == FlashMode.On   ? active : inactive);
        btnModeAuto.BackgroundColor = Color.FromArgb(_mode == FlashMode.Auto ? active : inactive);

        sliderPanel.IsVisible = _mode == FlashMode.On;
    }

    private void ApplyMode()
    {
        StopBackground();
        _light.Stop();

        switch (_mode)
        {
            case FlashMode.On:
                var halfPeriodMs = (long)(1000.0 / (frequencySlider.Value * 2));
                _light.SetDelay(halfPeriodMs);
                _light.Start();
                StartStrobeGuestLoop(halfPeriodMs);
                break;

            case FlashMode.Auto:
                StartAutoMode();
                break;
        }
    }

    private void OnFrequencyChanged(object sender, ValueChangedEventArgs e)
    {
        lblFrequency.Text = $"Frequency: {e.NewValue:F1} Hz";

        if (_mode == FlashMode.On)
        {
            var halfPeriodMs = (long)(1000.0 / (e.NewValue * 2));
            _light.SetDelay(halfPeriodMs);

            // Restart the guest strobe loop at the new rate
            StopBackground();
            StartStrobeGuestLoop(halfPeriodMs);
        }
    }

    // ─── On mode: strobe guests at the slider frequency ──────────────────────

    private void StartStrobeGuestLoop(long halfPeriodMs)
    {
        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;

        _ = Task.Run(async () =>
        {
            bool state = false;
            while (!token.IsCancellationRequested)
            {
                state = !state;
                await SendFlashToGuestsAsync(state ? "on" : "off");
                await Task.Delay((int)halfPeriodMs, token).ConfigureAwait(false);
            }
            await SendFlashToGuestsAsync("off");
        }, token);
    }

    // ─── Auto mode: audio-driven ──────────────────────────────────────────────

    private void StartAutoMode()
    {
        _audioAnalyser ??= new AudioAnalyser();
        _audioAnalyser.Init();

        _bgCts = new CancellationTokenSource();
        var token = _bgCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                double level  = _audioAnalyser.GetLowLevel();
                string action = level > 0.5 ? "on" : "off";

                // Drive both local flashlight and guests
                if (action == "on")
                    await Flashlight.Default.TurnOnAsync();
                else
                    await Flashlight.Default.TurnOffAsync();

                await SendFlashToGuestsAsync(action);
                await Task.Delay(50, token).ConfigureAwait(false);
            }
            await Flashlight.Default.TurnOffAsync();
            await SendFlashToGuestsAsync("off");
        }, token);
    }

    private void StopBackground()
    {
        _bgCts?.Cancel();
        _bgCts?.Dispose();
        _bgCts = null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Pad — press = light ON everywhere, release = light OFF everywhere
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnPadPressed(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: var param }) return;
        if (param is null)
            await Flashlight.Default.TurnOnAsync();                        // host pad
        else if (RoomSession.Current is { } room)
            try { await room.FlashGuestAsync((string)param, "on"); } catch { }
    }

    private async void OnPadReleased(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: var param }) return;
        if (param is null)
            await Flashlight.Default.TurnOffAsync();                       // host pad
        else if (RoomSession.Current is { } room)
            try { await room.FlashGuestAsync((string)param, "off"); } catch { }
    }

    private static async Task SendFlashToGuestsAsync(string action)
    {
        if (RoomSession.Current is { } room)
        {
            try { await room.FlashAsync(action); }
            catch { /* best-effort */ }
        }
    }

    private void RefreshPadGrid()
    {
        // Build the ordered device list: host always first
        var items = new List<(string Label, string? Ip)>
        {
            (DeviceInfo.Current.Name + "\n(You)", null),
        };
        foreach (var g in _guestInfos)
            items.Add((g.DisplayName, g.Ip));

        int n = items.Count;

        // Smart column count: favour square-ish grids
        int cols = n switch
        {
            1    => 1,
            <= 4 => 2,
            <= 9 => 3,
            _    => 4,
        };

        int rows       = (int)Math.Ceiling(n / (double)cols);
        int totalSlots = rows * cols;
        int leftover   = totalSlots - n;

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
                Text             = label,
                FontSize         = 11,
                LineBreakMode    = LineBreakMode.WordWrap,
                BackgroundColor  = PadColors[i % PadColors.Length],
                TextColor        = Colors.White,
                CornerRadius     = 12,
                CommandParameter = ip,
            };
            btn.Pressed  += OnPadPressed;
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
    // Toolbar — Invite (＋)
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnInviteToolbarClicked(object sender, EventArgs e)
    {
        if (_candidates.Count == 0)
        {
            await DisplayAlert("Invite", "No candidates discovered yet.\nMake sure other devices have the app open.", "OK");
            return;
        }

        string[] names = _candidates.Select(c => $"{c.GuestName}  ({c.GuestIp})").ToArray();
        string? choice = await DisplayActionSheet("Invite a guest", "Cancel", null, names);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var candidate = _candidates.FirstOrDefault(c => $"{c.GuestName}  ({c.GuestIp})" == choice);
        if (candidate is not null)
            await SendInviteAsync(candidate);
    }

    private async Task SendInviteAsync(GuestPresenceAnnouncement candidate)
    {
        if (RoomSession.Current is not { IsHost: true } room) return;
        if (_inviteDiscovery is null) return;

        try
        {
            var invite = new RoomInvite(
                InviteId:        Guid.NewGuid().ToString("N"),
                RoomId:          room.RoomId,
                RoomName:        room.RoomName,
                HostIp:          GuestIdentity.LocalIpv4(),
                TcpPort:         SocketRoomHost.TcpPort,
                ProtocolVersion: SspCbor.ProtocolVersion
            );
            await _inviteDiscovery.SendInviteAsync(invite, candidate.GuestIp);
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
        if (_guestInfos.Count == 0)
        {
            await DisplayAlert("Kick", "No guests are currently connected.", "OK");
            return;
        }

        string[] names = _guestInfos.Select(g => g.DisplayName).ToArray();
        string? choice = await DisplayActionSheet("Kick a guest", "Cancel", null, names);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var guest = _guestInfos.FirstOrDefault(g => g.DisplayName == choice);
        if (guest is not null)
            await KickGuestAsync(guest);
    }

    private async Task KickGuestAsync(GuestInfo guest)
    {
        if (RoomSession.Current is not { IsHost: true } room) return;

        try
        {
            bool kicked = await room.KickGuestAsync(guest.Ip);
            if (!kicked) { await DisplayAlert("Kick failed", "Guest is no longer connected.", "OK"); return; }

            var existing = _guestInfos.FirstOrDefault(x => x.Ip == guest.Ip);
            if (existing is not null) _guestInfos.Remove(existing);

            if (RoomSession.Current is { } current)
                RoomNotifications.SetHostStatus(current.RoomName, _guestInfos.Count);
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
        await RoomSession.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Guest session callbacks
    // ═════════════════════════════════════════════════════════════════════════

    private void OnGuestConnected(object? sender, GuestJoinedArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            GetOrAdd(args.Ip, args.Name);
            RemoveCandidateByIp(args.Ip);
            RefreshPadGrid();
            if (RoomSession.Current is { } room)
                RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
        });
    }

    private void OnGuestDisconnected(object? sender, string ip)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
            if (g is not null) _guestInfos.Remove(g);
            RefreshPadGrid();
            if (RoomSession.Current is { } room)
                RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
        });
    }

    private void OnGuestPingUpdated(object? sender, GuestPingArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() => GetOrAdd(args.Ip).RttMs = args.RttMs);
    }

    private void OnGuestPresenceDiscovered(object? sender, GuestPresenceAnnouncement presence)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!presence.Available) return;
            if (RoomSession.Current is not { IsHost: true }) return;
            if (_guestInfos.Any(g => g.Ip == presence.GuestIp))
            {
                RemoveCandidateByIp(presence.GuestIp);
                return;
            }

            var existing = _candidates.FirstOrDefault(c => c.GuestId == presence.GuestId || c.GuestIp == presence.GuestIp);
            if (existing is not null)
                _candidates[_candidates.IndexOf(existing)] = presence;
            else
                _candidates.Add(presence);
        });
    }

    private void OnInviteRefused(object? sender, InviteRefusal refusal) { /* silently ignored in POC */ }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    private GuestInfo GetOrAdd(string ip, string name = "")
    {
        var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
        if (g is not null) return g;
        var info = new GuestInfo { Ip = ip, Name = name };
        _guestInfos.Add(info);
        return info;
    }

    private void RemoveCandidateByIp(string ip)
    {
        var c = _candidates.FirstOrDefault(x => x.GuestIp == ip);
        if (c is not null) _candidates.Remove(c);
    }
}
