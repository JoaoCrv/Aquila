using Aquila.Models.Api;
using Aquila.Services.Providers;
using Aquila.Helpers;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Aquila.Services
{
    public class AquilaService : IDisposable
    {
        private readonly List<IDataProvider> _providers = new();
        private DispatcherTimer? _pollingTimer;
        private bool _disposed;

        public AquilaState State { get; } = new();

        public IReadOnlyList<IDataProvider> Providers => _providers;

        public event Action? DataUpdated;

        public void StartMonitoring()
        {
            if (_pollingTimer != null) return;

            // In the future this might be driven by Dependency Injection
            _providers.Add(new LhmProvider());

            foreach (var provider in _providers)
            {
                provider.Initialize();
            }

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pollingTimer.Tick += RefreshState;

            // Synchronous initial poll
            RefreshState(this, EventArgs.Empty);
            Console.WriteLine("LHM Data Populated");
            _pollingTimer.Start();
        }

        private void RefreshState(object? sender, EventArgs e)
        {
            foreach (var provider in _providers)
            {
                provider.Populate(State);
            }

            // Build a stable semantic snapshot so pages can bind to one curated object.
            AquilaSnapshotBuilder.PopulateSemantic(State);

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

            foreach (var provider in _providers)
            {
                provider.Dispose();
            }
            _providers.Clear();
        }
    }
}
