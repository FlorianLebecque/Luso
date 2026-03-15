#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Luso.Features.Rooms.Domain
{
    internal sealed class GuestInfo : INotifyPropertyChanged
    {
        private int _rttMs = -1;

        public string Ip { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;

        public string DisplayName => string.IsNullOrEmpty(Name) ? Ip : Name;

        public int RttMs
        {
            get => _rttMs;
            set { _rttMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingDisplay)); }
        }

        public string PingDisplay => _rttMs < 0 ? "…" : $"{_rttMs} ms";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

