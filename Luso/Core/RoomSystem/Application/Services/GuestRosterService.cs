#nullable enable
using System.Collections.ObjectModel;
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Services
{
    internal sealed class GuestRosterService : IGuestRosterService
    {
        private readonly Dictionary<string, (IDevice Device, EventHandler<int> Handler)> _latency = new();

        public ObservableCollection<GuestInfo> Guests { get; } = new();
        public ObservableCollection<IDiscoveredDevice> Candidates { get; } = new();

        public event EventHandler? GuestsChanged;

        // ── Room wiring ───────────────────────────────────────────────────────

        public void WireRoom(Room room)
        {
            room.OnDeviceConnected += OnDeviceConnected;
            room.OnDeviceDisconnected += OnDeviceDisconnected;
            room.OnCandidateDiscovered += OnCandidateDiscovered;

            foreach (var device in room.GetDevices())
            {
                GetOrAdd(device.DeviceId, device.DeviceName).RttMs = device.LatencyMs;
                SubscribeLatency(device);
            }
        }

        public void UnwireRoom(Room room)
        {
            room.OnDeviceConnected -= OnDeviceConnected;
            room.OnDeviceDisconnected -= OnDeviceDisconnected;
            room.OnCandidateDiscovered -= OnCandidateDiscovered;

            foreach (var (device, handler) in _latency.Values)
                device.OnLatencyUpdated -= handler;
            _latency.Clear();
        }

        public void RemoveGuest(string deviceId)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var g = Guests.FirstOrDefault(x => x.Ip == deviceId);
                if (g is not null) Guests.Remove(g);
                GuestsChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Clear()
        {
            Guests.Clear();
            Candidates.Clear();
        }

        // ── Room event callbacks ──────────────────────────────────────────────

        private void OnDeviceConnected(object? _, IDevice device)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GetOrAdd(device.DeviceId, device.DeviceName);
                SubscribeLatency(device);
                var joined = Candidates.FirstOrDefault(c => c.DeviceId == device.DeviceId);
                if (joined is not null) Candidates.Remove(joined);
                GuestsChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        private void OnDeviceDisconnected(object? _, IDevice device)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_latency.TryGetValue(device.DeviceId, out var entry))
                {
                    device.OnLatencyUpdated -= entry.Handler;
                    _latency.Remove(device.DeviceId);
                }
                var g = Guests.FirstOrDefault(x => x.Ip == device.DeviceId);
                if (g is not null) Guests.Remove(g);
                GuestsChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        private void OnCandidateDiscovered(object? _, IDiscoveredDevice device)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Guests.Any(g => g.Ip == device.Address))
                {
                    RemoveCandidateByAddress(device.Address);
                    return;
                }
                var existing = Candidates.FirstOrDefault(c =>
                    c.DeviceId == device.DeviceId || c.Address == device.Address);
                if (existing is not null)
                    Candidates[Candidates.IndexOf(existing)] = device;
                else
                    Candidates.Add(device);
            });
        }

        // ── Latency helpers ───────────────────────────────────────────────────

        private void SubscribeLatency(IDevice device)
        {
            if (_latency.ContainsKey(device.DeviceId)) return;
            EventHandler<int> handler = (_, rtt) =>
                MainThread.BeginInvokeOnMainThread(() => GetOrAdd(device.DeviceId).RttMs = rtt);
            _latency[device.DeviceId] = (device, handler);
            device.OnLatencyUpdated += handler;
        }

        // ── Collection helpers ────────────────────────────────────────────────

        private GuestInfo GetOrAdd(string ip, string name = "")
        {
            var g = Guests.FirstOrDefault(x => x.Ip == ip);
            if (g is not null) return g;
            var info = new GuestInfo { Ip = ip, Name = name };
            Guests.Add(info);
            return info;
        }

        private void RemoveCandidateByAddress(string address)
        {
            var c = Candidates.FirstOrDefault(x => x.Address == address);
            if (c is not null) Candidates.Remove(c);
        }
    }
}
