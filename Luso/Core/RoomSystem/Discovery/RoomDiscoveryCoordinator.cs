#nullable enable
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Infrastructure;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Owns the <see cref="IRoomScanner"/> lifecycle and re-exposes its events as
    /// clean typed callbacks.
    ///
    /// Used by both <c>HomePage</c> and <c>BrowseRoomsPage</c> to avoid duplicating
    /// scanner startup, teardown, and event wiring.
    ///
    /// Lifecycle: call <see cref="Start"/> in OnAppearing, <see cref="Stop"/> (or
    /// <see cref="Dispose"/>) in OnDisappearing.
    /// </summary>
    internal sealed class RoomDiscoveryCoordinator : IDisposable
    {
        /// <summary>A new room was announced on the LAN.</summary>
        public event EventHandler<IDiscoveredRoom>? RoomDiscovered;

        /// <summary>An incoming invite was received (accept or refuse via <see cref="RefuseInviteAsync"/>).</summary>
        public event EventHandler<IRoomInvite>? InviteReceived;

        private readonly IRoomTechnologyCatalog _catalog;
        private readonly Dictionary<string, IRoomScanner> _scanners = new(StringComparer.OrdinalIgnoreCase);

        public RoomDiscoveryCoordinator(IRoomTechnologyCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Starts scanning. Safe to call repeatedly (stops any existing scan first).</summary>
        public async void StartAsync()
        {
            Stop();
            foreach (var tech in _catalog.GetAll())
            {
                var scanner = tech.CreateScanner();
                if (scanner is null)
                    continue;

                scanner.OnRoomDiscovered += OnRoomDiscovered;
                scanner.OnInviteReceived += OnInviteReceived;
                await scanner.StartAsync();
                _scanners[tech.TechnologyId] = scanner;
            }
        }

        /// <summary>Stops scanning and disposes the underlying scanner.</summary>
        public void Stop()
        {
            if (_scanners.Count == 0) return;

            foreach (var scanner in _scanners.Values)
            {
                scanner.OnRoomDiscovered -= OnRoomDiscovered;
                scanner.OnInviteReceived -= OnInviteReceived;
                scanner.Stop();
                scanner.Dispose();
            }

            _scanners.Clear();
        }

        /// <summary>Sends an invite refusal through the active scanner.</summary>
        public Task RefuseInviteAsync(IRoomInvite invite, InviteRefuseReason reason)
        {
            if (_scanners.TryGetValue(invite.TechnologyId, out var scanner))
                return scanner.RefuseInviteAsync(invite, reason.ToWireString());

            return Task.CompletedTask;
        }

        public void Dispose() => Stop();

        private void OnRoomDiscovered(object? sender, IDiscoveredRoom room)
            => RoomDiscovered?.Invoke(this, room);

        private void OnInviteReceived(object? sender, IRoomInvite invite)
            => InviteReceived?.Invoke(this, invite);
    }

    /// <summary>Typed refusal reasons — mapped to wire strings at coordinator boundary.</summary>
    internal enum InviteRefuseReason
    {
        IncompatibleVersion,
        UserRefused,
    }

    internal static class InviteRefuseReasonExtensions
    {
        internal static string ToWireString(this InviteRefuseReason reason) => reason switch
        {
            InviteRefuseReason.IncompatibleVersion => "version",
            InviteRefuseReason.UserRefused => "user_refused",
            _ => "refused",
        };
    }
}
