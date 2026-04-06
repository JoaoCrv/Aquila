using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace Aquila.Services
{
    /// <summary>
    /// Small raw reader around LibreHardwareMonitor.
    /// Keeps the official Computer + UpdateVisitor polling pattern isolated from the rest of the app.
    /// </summary>
    public sealed class LibreHardwareReader : IDisposable
    {
        private readonly Computer _computer;
        private readonly UpdateVisitor _updateVisitor = new();
        private bool _isOpen;
        private bool _disposed;

        public LibreHardwareReader()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };
        }

        public void Open()
        {
            ThrowIfDisposed();

            if (_isOpen)
                return;

            _computer.Open();
            _isOpen = true;
        }

        public IEnumerable<IHardware> ReadAllHardware()
        {
            ThrowIfDisposed();
            Open();

            _computer.Accept(_updateVisitor);
            return _computer.Hardware;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_isOpen)
            {
                _computer.Close();
                _isOpen = false;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();

                foreach (IHardware subHardware in hardware.SubHardware)
                    subHardware.Accept(this);
            }

            public void VisitSensor(ISensor sensor) { }

            public void VisitParameter(IParameter parameter) { }
        }
    }
}
