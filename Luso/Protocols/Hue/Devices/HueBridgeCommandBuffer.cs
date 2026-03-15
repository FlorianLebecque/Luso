#nullable enable
using HueApi;
using HueApi.Models.Requests;
using System.Collections.Concurrent;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Per-bridge command multiplexer that prevents congestion on the Hue bridge.
    ///
    /// Problem: firing N concurrent HTTP requests (one per bulb) overwhelms the
    /// bridge's ~10 req/s rate limit, producing 1–2 second lag for large setups.
    ///
    /// Solution — "latest-wins, one in-flight" per light:
    ///   • At most ONE outstanding HTTP request per bulb at any time.
    ///   • While a request is in-flight, any new command is held in a single
    ///     "pending" slot. A newer command silently replaces the older pending one
    ///     (we only care about the final state, not intermediate steps).
    ///   • Once the in-flight request completes, the pending slot (if any) is
    ///     dispatched immediately.
    ///
    /// All lights still fire in parallel — there is no artificial serialisation
    /// across lights, only per-light back-pressure.
    /// </summary>
    internal sealed class HueBridgeCommandBuffer : IDisposable
    {
        private sealed class LightSlot
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public volatile UpdateLight? Pending;
        }

        private readonly LocalHueApi _api;
        private readonly ConcurrentDictionary<Guid, LightSlot> _slots = new();

        internal HueBridgeCommandBuffer(LocalHueApi api) => _api = api;

        /// <summary>
        /// Schedules <paramref name="update"/> for <paramref name="lightId"/>.
        /// If a request is already in-flight the update is queued as pending
        /// (replacing any previously queued update). If the light is idle the
        /// request fires immediately.
        /// </summary>
        internal void Schedule(Guid lightId, UpdateLight update)
        {
            var slot = _slots.GetOrAdd(lightId, _ => new LightSlot());

            // Always write the latest desired state.
            slot.Pending = update;

            // If the gate is available the light is idle — grab it and dispatch.
            // If not, the in-flight completion loop will pick up the pending value.
            if (slot.Gate.CurrentCount > 0)
                _ = DrainSlotAsync(slot, lightId);
        }

        private async Task DrainSlotAsync(LightSlot slot, Guid lightId)
        {
            // Try to acquire the gate; if another DrainSlotAsync just won the
            // race, bail — it will see our Pending write.
            if (!await slot.Gate.WaitAsync(0).ConfigureAwait(false))
                return;

            try
            {
                // Keep draining as long as there is a pending command.
                while (slot.Pending is { } update)
                {
                    slot.Pending = null; // consume

                    try
                    {
                        await _api.Light.UpdateAsync(lightId, update).ConfigureAwait(false);
                    }
                    catch { /* swallow — strobe must not crash */ }
                }
            }
            finally
            {
                slot.Gate.Release();

                // One last check: a Schedule() call may have set Pending between
                // our while-exit and the Release, and seen Gate == 0, so it
                // didn't start a new drain. Kick off a new one if needed.
                if (slot.Pending is not null && slot.Gate.CurrentCount > 0)
                    _ = DrainSlotAsync(slot, lightId);
            }
        }

        public void Dispose()
        {
            // Slots are lightweight; nothing long-lived to cancel.
            _slots.Clear();
        }
    }
}
