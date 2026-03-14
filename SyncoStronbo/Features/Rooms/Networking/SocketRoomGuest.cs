#nullable enable
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SyncoStronbo.Features.Rooms.Networking {
    internal sealed class SocketRoomGuest : IDisposable {
        private const int PingTimeoutMs = 6000;

        private readonly TcpClient _client;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private volatile bool _leavingVoluntarily;
        private long _lastPingAtMs;
        private bool _watchdogStarted;

        public string HostIp { get; }
        public string RoomId { get; }
        public string RoomName { get; }
        public bool IsConnected => _client.Connected;

        public event EventHandler<FlashCommand>? OnFlashCommand;
        public event EventHandler? OnDisconnected;

        private SocketRoomGuest(TcpClient client, RoomAnnouncement room) {
            _client = client;
            HostIp = room.HostIp;
            RoomId = room.RoomId;
            RoomName = room.RoomName;
            _ = ListenLoopAsync(_cts.Token);
        }

        public static async Task<SocketRoomGuest> ConnectAsync(RoomAnnouncement room, int timeoutMs = 5000) {
            var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(room.HostIp, room.TcpPort, cts.Token);
            return new SocketRoomGuest(client, room);
        }

        private async Task ListenLoopAsync(CancellationToken token) {
            bool hostClosed = false;
            try {
                using var reader = new StreamReader(_client.GetStream(), Encoding.UTF8, leaveOpen: true);
                while (!token.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(token);
                    if (line is null) break;
                    hostClosed = await HandleLineAsync(line);
                    if (hostClosed) break;
                }
            } catch (OperationCanceledException) {
                return;
            } catch {
            }

            if (!_leavingVoluntarily)
                OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        private async Task WatchdogLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try { await Task.Delay(2000, token); } catch (OperationCanceledException) { return; }

                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPingAtMs;
                if (elapsed > PingTimeoutMs) {
                    if (!_leavingVoluntarily)
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        private async Task<bool> HandleLineAsync(string json) {
            try {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Type", out var typeProp)) return false;

                switch (typeProp.GetString()) {
                    case "PING":
                        long sentAt = root.GetProperty("SentAtMs").GetInt64();
                        _lastPingAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (!_watchdogStarted) {
                            _watchdogStarted = true;
                            _ = WatchdogLoopAsync(_cts.Token);
                        }
                        await WriteAsync(JsonSerializer.Serialize(new { Type = "PONG", SentAtMs = sentAt }) + "\n");
                        break;
                    case "FLASH":
                        OnFlashCommand?.Invoke(this, new FlashCommand(
                            Action: root.GetProperty("Action").GetString()!,
                            AtUnixMs: root.GetProperty("AtUnixMs").GetInt64()
                        ));
                        break;
                    case "CLOSE":
                        return true;
                }
            } catch {
            }
            return false;
        }

        private async Task WriteAsync(string line) {
            var bytes = Encoding.UTF8.GetBytes(line);
            await _writeLock.WaitAsync();
            try {
                await _client.GetStream().WriteAsync(bytes);
            } finally {
                _writeLock.Release();
            }
        }

        public void Dispose() {
            _leavingVoluntarily = true;
            try {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Type = "LEAVE" }) + "\n");
                _client.GetStream().Write(bytes);
            } catch {
            }

            _cts.Cancel();
            _client.Dispose();
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }
}
