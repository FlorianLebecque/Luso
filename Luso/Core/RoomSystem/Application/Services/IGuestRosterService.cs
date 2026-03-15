#nullable enable
using System.Collections.ObjectModel;
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Manages the guest list, candidate list, and latency subscriptions for the host room.
    /// Extracted from <c>HostRoomPage</c> to keep the page focused on UI wiring.
    /// </summary>
    internal interface IGuestRosterService
    {
        /// <summary>Currently connected guests (observable; safe to bind to UI).</summary>
        ObservableCollection<GuestInfo> Guests { get; }

        /// <summary>Discovered but not-yet-invited devices (observable; safe to bind to UI).</summary>
        ObservableCollection<IDiscoveredDevice> Candidates { get; }

        /// <summary>Fired on the main thread when the guest list changes (connect / disconnect / kick).</summary>
        event EventHandler? GuestsChanged;

        /// <summary>
        /// Subscribes to room device events and pre-populates collections from existing devices.
        /// Call from <c>OnAppearing</c>.
        /// </summary>
        void WireRoom(Room room);

        /// <summary>
        /// Unsubscribes from room device events.
        /// Call from <c>OnDisappearing</c>.
        /// </summary>
        void UnwireRoom(Room room);

        /// <summary>Removes a guest by device ID and fires <see cref="GuestsChanged"/> (optimistic kick UI).</summary>
        void RemoveGuest(string deviceId);

        /// <summary>Clears all collections. Call after navigation away.</summary>
        void Clear();
    }
}
