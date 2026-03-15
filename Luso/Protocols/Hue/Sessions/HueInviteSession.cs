#nullable enable
using System.Text;
using System.Text.Json;
using HueApi;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Domain.Technologies;
using Zeroconf;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Host-side invite session for the Hue Bridge technology.
    ///
    /// Discovery: on <see cref="Start"/> a fast-path probe fires against any cached
    /// bridge IPs (≤0 seconds when already known). If nothing is found the full
    /// LAN scan runs as fallback (HTTP probe of every /24 IP).
    ///
    /// Pairing: <see cref="SendInviteAsync"/> uses a stored API key when available,
    /// otherwise polls the link-button for up to 30 s. On success it enumerates
    /// all lights and injects a <see cref="HueBridgeDevice"/> into the host session,
    /// each bulb backed by a shared <see cref="HueBridgeCommandBuffer"/>.
    /// </summary>
    internal sealed class HueInviteSession : IInviteSession
    {
        private const string HueMdnsService = "_hue._tcp.local.";

        private readonly HueHostSession _hostSession;
        private CancellationTokenSource? _discoveryCts;

        // 500 ms timeout; reused to avoid socket exhaustion.
        private static readonly System.Net.Http.HttpClient _probeClient =
            new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };

        public event EventHandler<IDiscoveredDevice>? OnDevicePresenceDiscovered;
        public event EventHandler<string>? OnInviteRefused;   // never raised for Hue

        internal HueInviteSession(HueHostSession hostSession) => _hostSession = hostSession;

        // ── IInviteSession lifecycle ────────────────────────────────────────────────

        public void Start()
        {
            _discoveryCts?.Cancel();
            _discoveryCts = new CancellationTokenSource();
            _ = Task.Run(() => ScanLoopAsync(_discoveryCts.Token));
        }

        public void Stop()
        {
            _discoveryCts?.Cancel();
            _discoveryCts = null;
        }

        public void Dispose() => Stop();

        // ── IInviteSession invite ───────────────────────────────────────────────

        public async Task SendInviteAsync(IDiscoveredDevice device, string roomId, string roomName)
        {
            if (device is not HueDiscoveredDevice hue) return;

            var apiKey = HueBridgeAuth.GetApiKey(hue.DeviceId)
                      ?? await PollForPairingAsync(hue.Address, hue.DeviceId).ConfigureAwait(false);

            if (apiKey is null) return;
            await ConnectBridgeAsync(hue.DeviceId, hue.Address, apiKey).ConfigureAwait(false);
        }

        // ── Scan loop ───────────────────────────────────────────────────────────

        private async Task ScanLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await DiscoverBridgesAsync(ct).ConfigureAwait(false);
                try { await Task.Delay(TimeSpan.FromSeconds(7), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task DiscoverBridgesAsync(CancellationToken ct)
        {
            var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);

            void Announce(string bridgeId, string ip)
            {
                if (seen.TryAdd(ip, true))
                    OnDevicePresenceDiscovered?.Invoke(this, new HueDiscoveredDevice(bridgeId, ip));
            }

            // ── Fast-path: probe cached IPs (≤ 500 ms) ─────────────────────────────
            var known = HueBridgeAuth.GetKnownBridges();
            if (known.Count > 0)
            {
                await Task.WhenAll(known.Select(async b =>
                {
                    try
                    {
                        var r = await _probeClient
                            .GetAsync($"http://{b.Ip}/api/config", ct).ConfigureAwait(false);
                        if (r.IsSuccessStatusCode) Announce(b.BridgeId, b.Ip);
                    }
                    catch { }
                })).ConfigureAwait(false);

                if (!seen.IsEmpty) return; // cached bridges still alive — skip mDNS
            }

            // ── mDNS (fallback when no cached hit) ──────────────────────────────────
            using var _ = HueMulticastLock.Acquire();
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var results = await ZeroconfResolver
                    .ResolveAsync(HueMdnsService, cancellationToken: linked.Token)
                    .ConfigureAwait(false);

                foreach (var host in results)
                {
                    // TXT record "bridgeid" contains the bridge ID; fall back to display name.
                    string? bridgeId = null;
                    var props = host.Services.GetValueOrDefault(HueMdnsService)?.Properties.FirstOrDefault();
                    props?.TryGetValue("bridgeid", out bridgeId);

                    Announce(bridgeId ?? host.DisplayName, host.IPAddress);
                }
            }
            catch { }
        }

        // ── Pairing ───────────────────────────────────────────────────────────────

        // SSL-bypassing client for bridge HTTPS (self-signed cert)
        private static readonly HttpClient _pairClient = new(
            new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });

        private static async Task<string?> PollForPairingAsync(string ip, string bridgeId)
        {
            for (int i = 1; i <= 30; i++)
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, $"https://{ip}/api");
                    req.Content = new StringContent(
                        "{\"devicetype\":\"luso#phone\",\"generateclientkey\":true}",
                        Encoding.UTF8, "application/json");

                    var resp = await _pairClient.SendAsync(req).ConfigureAwait(false);
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // Response: [{"success":{"username":"...","clientkey":"..."}}]
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Array) goto next;

                    foreach (var entry in root.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("success", out var success)) continue;

                        if (!success.TryGetProperty("username", out var userEl)) continue;
                        var username = userEl.GetString();
                        if (string.IsNullOrEmpty(username)) continue;

                        HueBridgeAuth.StoreApiKey(bridgeId, username);

                        if (success.TryGetProperty("clientkey", out var ckEl))
                        {
                            var ck = ckEl.GetString();
                            if (!string.IsNullOrEmpty(ck))
                                HueBridgeAuth.StoreClientKey(bridgeId, ck);
                        }

                        return username;
                    }
                }
                catch (HttpRequestException) { /* bridge not reachable yet */ }
                catch { return null; }

            next:
                await Task.Delay(1000).ConfigureAwait(false);
            }
            return null;
        }

        // Returns the cached hue-application-id, fetching it from the bridge if not yet stored.
        private static async Task<string?> GetOrFetchApplicationIdAsync(string ip, string bridgeId, string apiKey)
        {
            var cached = HueBridgeAuth.GetApplicationId(bridgeId);
            if (cached is not null) return cached;

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/auth/v1");
                req.Headers.Add("hue-application-key", apiKey);
                var resp = await _pairClient.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                if (resp.Headers.TryGetValues("hue-application-id", out var vals))
                {
                    var appId = vals.FirstOrDefault();
                    if (!string.IsNullOrEmpty(appId))
                    {
                        HueBridgeAuth.StoreApplicationId(bridgeId, appId);
                        return appId;
                    }
                }
            }
            catch { }
            return null;
        }

        // ── Connect ────────────────────────────────────────────────────────────────

        private async Task ConnectBridgeAsync(string bridgeId, string ip, string apiKey)
        {
            try
            {
                var api = new LocalHueApi(ip, apiKey);
                HueBridgeAuth.StoreKnownBridge(bridgeId, ip);

                var targets = new List<ITarget>();
                var entertainmentSessions = new List<HueEntertainmentSession>();

                // ── Try Entertainment API (all zones) ──────────────────────────
                var clientKey = HueBridgeAuth.GetClientKey(bridgeId);
                var appId = await GetOrFetchApplicationIdAsync(ip, bridgeId, apiKey).ConfigureAwait(false);

                if (clientKey is not null && appId is not null)
                {
                    var sessions = await HueEntertainmentSession
                        .CreateAllAsync(ip, apiKey, clientKey, appId).ConfigureAwait(false);

                    foreach (var session in sessions)
                    {
                        entertainmentSessions.Add(session);
                        foreach (var chId in session.ChannelIds)
                            targets.Add(new HueEntertainmentTarget(session, chId, $"Zone ch{chId}"));
                    }
                }

                // ── Fallback: REST targets for individual lights ────────────────
                if (targets.Count == 0)
                {
                    var buffer = new HueBridgeCommandBuffer(api);
                    var response = await api.Light.GetAllAsync().ConfigureAwait(false);
                    targets.AddRange(response.Data.Select(l => (ITarget)new HueLightTarget(
                        l.Id,
                        l.Metadata?.Name ?? l.ProductData?.Name ?? $"Light {l.IdV1}",
                        buffer)));
                }

                _hostSession.AddDevice(new HueBridgeDevice(
                    bridgeId, ip, targets,
                    id =>
                    {
                        // Dispose all entertainment sessions when device is removed
                        foreach (var s in entertainmentSessions)
                            _ = s.DisposeAsync().AsTask();
                        _hostSession.RemoveDevice(id);
                    }));
            }
            catch { }
        }
    }
}
