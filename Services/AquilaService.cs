using Aquila.Models.Api;
using Aquila.Services.Providers;
using Aquila.Services.Translators;
using LibreHardwareMonitor.Hardware;
using System;
using System.Windows.Threading;

namespace Aquila.Services
{
    public class AquilaService : IDisposable
    {
        private LhmProvider? _lhm;
        private LhmSemanticTranslator? _translator;
        private DispatcherTimer? _pollingTimer;
        private bool _disposed;

        public AquilaSemanticState State { get; } = new();

        public IComputer? Computer => _lhm?.Computer;

        public event Action? DataUpdated;

        public void StartMonitoring()
        {
            if (_pollingTimer != null) return;

            _lhm = new LhmProvider();
            _lhm.Initialize();

            _translator = new LhmSemanticTranslator(_lhm.Computer);

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pollingTimer.Tick += RefreshState;

            // Synchronous initial poll
            RefreshState(this, EventArgs.Empty);
            Console.WriteLine("LHM Data Populated");
            _pollingTimer.Start();
        }

        private void RefreshState(object? sender, EventArgs e)
        {
            _translator?.Update(State);
            DataUpdated?.Invoke();
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

            _lhm?.Dispose();
            _lhm = null;
        }
    }
}
