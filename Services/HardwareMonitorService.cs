using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Aquila.Services
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    /// <summary>
    /// The ONE and ONLY hardware service. Reads raw data and transforms it.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private Computer? _computer;
        private DispatcherTimer? _timer;
        private bool _disposed;

        private PerformanceCounter? _pageReadCounter;
        private PerformanceCounter? _pageWriteCounter;
        private PerformanceCounter? _cacheCounter;

        public event Action? DataUpdated;
        public ComputerData ComputerData { get; } = new();

        // ── Windows memory extras ────────────────────────────────────────
        /// <summary>Page reads per second (paging file read activity).</summary>
        public float PageReadsPerSec  { get; private set; }
        /// <summary>Page writes per second (paging file write activity).</summary>
        public float PageWritesPerSec { get; private set; }
        /// <summary>System file cache size in bytes.</summary>
        public long  CacheBytes       { get; private set; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint  cb, CommitTotal, CommitLimit, CommitPeak,
                         PhysicalTotal, PhysicalAvailable, SystemCache,
                         KernelTotal, KernelPaged, KernelNonpaged, PageSize,
                         HandleCount, ProcessCount, ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        public void StartMonitoring()
        {
            if (_computer != null) return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += UpdateDataModel;

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };

            try
            {
                _computer.Open();
                _timer.Start();

                _pageReadCounter  = new PerformanceCounter("Memory", "Page Reads/sec",  readOnly: true);
                _pageWriteCounter = new PerformanceCounter("Memory", "Page Writes/sec", readOnly: true);
                _cacheCounter     = new PerformanceCounter("Memory", "Cache Bytes",     readOnly: true);
                // First call returns 0 — discard it
                _pageReadCounter.NextValue();
                _pageWriteCounter.NextValue();
                _cacheCounter.NextValue();
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

            _timer?.Stop();
            _timer = null;

            _computer?.Close();
            _computer = null;

            _pageReadCounter?.Dispose();
            _pageWriteCounter?.Dispose();
            _cacheCounter?.Dispose();
        }

        private void UpdateDataModel(object? sender, EventArgs e)
        {
            if (_computer == null) return;

            _computer.Accept(new UpdateVisitor());

            foreach (var rawHardware in _computer.Hardware)
            {
                var hardwareNode = ComputerData.HardwareList.FirstOrDefault(h => h.Identifier == rawHardware.Identifier.ToString());
                if (hardwareNode == null)
                {
                    hardwareNode = new DataHardware(rawHardware.Identifier.ToString(), rawHardware.Name, rawHardware.HardwareType);
                    ComputerData.HardwareList.Add(hardwareNode);
                }

                var allSensors = rawHardware.Sensors.Concat(rawHardware.SubHardware.SelectMany(s => s.Sensors));
                foreach (var rawSensor in allSensors)
                {
                    var sensorId = rawSensor.Identifier.ToString();
                    if (!ComputerData.SensorIndex.TryGetValue(sensorId, out var dataSensor))
                    {
                        dataSensor = new DataSensor(
                            rawSensor.Index,
                            sensorId,
                            rawSensor.Name,
                            rawSensor.SensorType,
                            GetSensorUnit(rawSensor.SensorType));
                        ComputerData.SensorIndex[sensorId] = dataSensor;
                        hardwareNode.Sensors.Add(dataSensor);
                    }
                    dataSensor.Value = rawSensor.Value ?? 0;
                    dataSensor.Min = rawSensor.Min ?? 0;
                    dataSensor.Max = rawSensor.Max ?? 0;
                }
            }

            // ── Update Windows memory extras ──────────────────────────────────────────────────────────────────
            try
            {
                if (GetPerformanceInfo(out var pi, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
                    CacheBytes = (long)pi.SystemCache * (long)pi.PageSize;

                PageReadsPerSec  = _pageReadCounter?.NextValue()  ?? 0;
                PageWritesPerSec = _pageWriteCounter?.NextValue() ?? 0;
            }
            catch { }

            DataUpdated?.Invoke();
        }

        private static string GetSensorUnit(SensorType type) => type switch
        {
            SensorType.Temperature => "°C",
            SensorType.Load => "%",
            SensorType.Clock => "MHz",
            SensorType.Power => "W",
            SensorType.Fan => "RPM",
            SensorType.Data => "GB",
            SensorType.SmallData => "MB",
            SensorType.Throughput => "B/s",
            SensorType.Voltage => "V",
            SensorType.Frequency => "Hz",
            SensorType.Control => "%",
            _ => string.Empty
        };
    }
}