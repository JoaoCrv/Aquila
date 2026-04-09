using Aquila.Helpers;
using Aquila.Models;
using System;
using System.Windows.Threading;

namespace Aquila.Services
{
    /// <summary>
    /// Orchestrates hardware polling, raw-model synchronization, and semantic snapshot creation for the app.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private readonly LibreHardwareDataMapper _libreHardwareDataMapper = new();
        private LibreHardwareMonitorReader? _libreHardwareReader;
        private WindowsMemoryMetricsReader? _windowsMemoryReader;
        private DispatcherTimer? _pollingTimer;
        private bool _disposed;

        public event Action? DataUpdated;
        public HardwareState RawHardwareState { get; } = new();
        public AquilaSnapshot CurrentSnapshot { get; private set; } = new();

        // ── Windows memory extras ────────────────────────────────────────
        /// <summary>Page reads per second (paging file read activity).</summary>
        public float PageReadsPerSec { get; private set; }
        /// <summary>Page writes per second (paging file write activity).</summary>
        public float PageWritesPerSec { get; private set; }
        /// <summary>System file cache size in bytes.</summary>
        public long CacheBytes { get; private set; }

        public void StartMonitoring()
        {
            if (_libreHardwareReader != null) return;

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pollingTimer.Tick += RefreshState;
            _libreHardwareReader = new LibreHardwareMonitorReader();
            _windowsMemoryReader = new WindowsMemoryMetricsReader();

            try
            {
                _libreHardwareReader.Open();
                _windowsMemoryReader.Open();
                RefreshState(this, EventArgs.Empty);
                _pollingTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Failed to start: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Tick -= RefreshState;
                _pollingTimer = null;
            }

            _libreHardwareReader?.Dispose();
            _libreHardwareReader = null;

            _windowsMemoryReader?.Dispose();
            _windowsMemoryReader = null;
        }

        private void RefreshState(object? sender, EventArgs e)
        {
            if (_libreHardwareReader == null) return;

            _libreHardwareDataMapper.UpdateFromHardware(RawHardwareState, _libreHardwareReader.ReadAllHardware());

            if (_windowsMemoryReader is { } memoryMetricsReader)
            {
                var metricsSnapshot = memoryMetricsReader.ReadSnapshot();
                CacheBytes = metricsSnapshot.CacheBytes;
                PageReadsPerSec = metricsSnapshot.PageReadsPerSec;
                PageWritesPerSec = metricsSnapshot.PageWritesPerSec;
            }

            CurrentSnapshot = AquilaSnapshotBuilder.Build(RawHardwareState, PageReadsPerSec, PageWritesPerSec, CacheBytes);
            DataUpdated?.Invoke();
        }

    }
}