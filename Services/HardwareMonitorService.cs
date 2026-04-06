using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Linq;
using System.Windows.Threading;

namespace Aquila.Services
{
    /// <summary>
    /// The ONE and ONLY hardware service. Reads raw data and transforms it.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private LibreHardwareReader? _hardwareReader;
        private WindowsMetricsReader? _windowsMetricsReader;
        private DispatcherTimer? _timer;
        private bool _disposed;

        public event Action? DataUpdated;
        public ComputerData ComputerData { get; } = new();

        // ── Windows memory extras ────────────────────────────────────────
        /// <summary>Page reads per second (paging file read activity).</summary>
        public float PageReadsPerSec { get; private set; }
        /// <summary>Page writes per second (paging file write activity).</summary>
        public float PageWritesPerSec { get; private set; }
        /// <summary>System file cache size in bytes.</summary>
        public long CacheBytes { get; private set; }

        public void StartMonitoring()
        {
            if (_hardwareReader != null) return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += UpdateDataModel;
            _hardwareReader = new LibreHardwareReader();
            _windowsMetricsReader = new WindowsMetricsReader();

            try
            {
                _hardwareReader.Open();
                _windowsMetricsReader.Open();
                _timer.Start();
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

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= UpdateDataModel;
                _timer = null;
            }

            _hardwareReader?.Dispose();
            _hardwareReader = null;

            _windowsMetricsReader?.Dispose();
            _windowsMetricsReader = null;
        }

        private void UpdateDataModel(object? sender, EventArgs e)
        {
            if (_hardwareReader == null) return;

            foreach (var rawHardware in _hardwareReader.ReadAllHardware())
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

            if (_windowsMetricsReader is { } windowsMetricsReader)
            {
                var metricsSnapshot = windowsMetricsReader.ReadSnapshot();
                CacheBytes = metricsSnapshot.CacheBytes;
                PageReadsPerSec = metricsSnapshot.PageReadsPerSec;
                PageWritesPerSec = metricsSnapshot.PageWritesPerSec;
            }

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