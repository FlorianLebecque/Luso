#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncoStronbo {

    /// <summary>
    /// Bindable model representing a connected guest in the host's room view.
    /// Implements INotifyPropertyChanged so CollectionView updates live.
    /// </summary>
    internal sealed class GuestInfo : INotifyPropertyChanged {

        private int    _rttMs  = -1;

        public string Ip { get; init; } = string.Empty;

        /// <summary>Last measured round-trip time in milliseconds. -1 = no reply yet.</summary>
        public int RttMs {
            get => _rttMs;
            set { _rttMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingDisplay)); }
        }

        public string PingDisplay => _rttMs < 0 ? "…" : $"{_rttMs} ms";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
