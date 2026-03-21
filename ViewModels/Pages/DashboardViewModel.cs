using Aquila.Helpers;
using Aquila.Models;
using Aquila.Services;
using LibreHardwareMonitor.Hardware;
using System.Linq;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService;

        public ComputerData Computer => _monitorService.ComputerData;

        [ObservableProperty]
        private float _effectiveCpuClock;

        // ── Gauge values (0–100) ─────────────────────────────────────────
        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _gpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;

        // ── CPU ──────────────────────────────────────────────────────────
        public string? CpuName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name;
        public DataSensor? CpuTemperatureSensor => SensorLocator.CpuTemperature(Computer);
        public DataSensor? CpuUsageSensor => SensorLocator.CpuLoad(Computer);
        public DataSensor? CpuEnergySensor => SensorLocator.CpuPower(Computer);
        public DataSensor? CpuFanSpeed1Sensor => SensorLocator.CpuFan(Computer, 0);
        public DataSensor? CpuFanSpeed2Sensor => SensorLocator.CpuFan(Computer, 1);

        // ── GPU ──────────────────────────────────────────────────────────
        public string? GpuName => SensorLocator.DetectGpuType(Computer) is { } gpuType
            ? Computer.HardwareList.FirstOrDefault(h => h.HardwareType == gpuType)?.Name
            : null;
        public DataSensor? GpuTemperatureSensor => SensorLocator.GpuTemperature(Computer);
        public DataSensor? GpuUsageSensor => SensorLocator.GpuLoad(Computer);
        public DataSensor? GpuSpeedSensor => SensorLocator.GpuClock(Computer);
        public DataSensor? GpuEnergySensor => SensorLocator.GpuPower(Computer);
        public DataSensor? GpuFanSpeed1Sensor => SensorLocator.GpuFan(Computer, 0);
        public DataSensor? GpuFanSpeed2Sensor => SensorLocator.GpuFan(Computer, 1);

        // ── RAM ──────────────────────────────────────────────────────────
        public string? MemoryName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Memory)?.Name;
        public DataSensor? MemoryUsageSensor => SensorLocator.MemoryLoad(Computer);
        public DataSensor? MemoryUsedSensor => SensorLocator.MemoryUsed(Computer);
        public DataSensor? MemoryAvailableSensor => SensorLocator.MemoryAvailable(Computer);
        public DataSensor? VirtualMemoryUsageSensor => SensorLocator.VirtualMemoryLoad(Computer);
        public DataSensor? VirtualMemoryUsedSensor => SensorLocator.VirtualMemoryUsed(Computer);
        public DataSensor? VirtualMemoryAvailableSensor => SensorLocator.VirtualMemoryAvailable(Computer);

        // ── Network ──────────────────────────────────────────────────────
        public string? NetworkName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Network)?.Name;
        public DataSensor? NetworkUploadSpeedSensor => SensorLocator.NetworkUploadSpeed(Computer);
        public DataSensor? NetworkDownloadSpeedSensor => SensorLocator.NetworkDownloadSpeed(Computer);
        public DataSensor? NetworkDataUploadedSensor => SensorLocator.NetworkDataUploaded(Computer);
        public DataSensor? NetworkDataDownloadedSensor => SensorLocator.NetworkDataDownloaded(Computer);

        private bool _sensorsResolved = false;

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

            _monitorService.DataUpdated += () =>
            {
                CalculateEffectiveCpuClock();

                CpuGaugeValue = CpuUsageSensor?.Value ?? 0;
                GpuGaugeValue = GpuUsageSensor?.Value ?? 0;
                RamGaugeValue = MemoryUsageSensor?.Value ?? 0;

                OnPropertyChanged(nameof(CpuName));
                OnPropertyChanged(nameof(GpuName));
                OnPropertyChanged(nameof(MemoryName));
                OnPropertyChanged(nameof(NetworkName));

                if (!_sensorsResolved)
                    NotifySensorReferences();
            };
        }

        private void CalculateEffectiveCpuClock()
        {
            var cpu = Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) { EffectiveCpuClock = 0; return; }

            var clockSensors = cpu.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core #")).ToList();
            var loadSensors = cpu.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core #")).ToList();

            float maxEffectiveClock = 0;
            foreach (var clockSensor in clockSensors)
            {
                var coreNumber = clockSensor.Name.Replace("Core #", "");
                var correspondingLoadSensor = loadSensors.FirstOrDefault(s => s.Name.EndsWith(coreNumber, StringComparison.Ordinal));
                if (correspondingLoadSensor != null)
                {
                    float effectiveClock = clockSensor.Value * (correspondingLoadSensor.Value / 100);
                    if (effectiveClock > maxEffectiveClock)
                        maxEffectiveClock = effectiveClock;
                }
            }
            EffectiveCpuClock = maxEffectiveClock;
        }

        private void NotifySensorReferences()
        {
            OnPropertyChanged(nameof(CpuTemperatureSensor));
            OnPropertyChanged(nameof(CpuUsageSensor));
            OnPropertyChanged(nameof(CpuEnergySensor));
            OnPropertyChanged(nameof(CpuFanSpeed1Sensor));
            OnPropertyChanged(nameof(CpuFanSpeed2Sensor));

            OnPropertyChanged(nameof(GpuUsageSensor));
            OnPropertyChanged(nameof(GpuTemperatureSensor));
            OnPropertyChanged(nameof(GpuSpeedSensor));
            OnPropertyChanged(nameof(GpuEnergySensor));
            OnPropertyChanged(nameof(GpuFanSpeed1Sensor));
            OnPropertyChanged(nameof(GpuFanSpeed2Sensor));

            OnPropertyChanged(nameof(MemoryUsageSensor));
            OnPropertyChanged(nameof(MemoryAvailableSensor));
            OnPropertyChanged(nameof(MemoryUsedSensor));
            OnPropertyChanged(nameof(VirtualMemoryUsageSensor));
            OnPropertyChanged(nameof(VirtualMemoryAvailableSensor));
            OnPropertyChanged(nameof(VirtualMemoryUsedSensor));

            OnPropertyChanged(nameof(NetworkUploadSpeedSensor));
            OnPropertyChanged(nameof(NetworkDownloadSpeedSensor));
            OnPropertyChanged(nameof(NetworkDataUploadedSensor));
            OnPropertyChanged(nameof(NetworkDataDownloadedSensor));

            _sensorsResolved =
                CpuTemperatureSensor != null &&
                CpuUsageSensor != null &&
                GpuTemperatureSensor != null &&
                MemoryUsageSensor != null;
        }
    }
}