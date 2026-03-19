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

        private void CalculateEffectiveCpuClock()
        {
            var cpu = Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null)
            {
                EffectiveCpuClock = 0;
                return;
            }

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

        // CPU
        public string? CpuName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name;
        public DataSensor? CpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? CpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/load/0");
        public DataSensor? CpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/power/0");
        public DataSensor? CpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/0");
        public DataSensor? CpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/1");

        // GPU
        public string? GpuName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.GpuAmd)?.Name;
        public DataSensor? GpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/load/0");
        public DataSensor? GpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/temperature/0");
        public DataSensor? GpuSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/clock/0");
        public DataSensor? GpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/power/3");
        public DataSensor? GpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/fan/0");
        public DataSensor? GpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/0/fan/1");

        // RAM
        public string? MemoryName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Memory)?.Name;
        public DataSensor? MemoryUsageSensor => Computer.SensorIndex.GetValueOrDefault("/ram/load/0");
        public DataSensor? MemoryAvailableSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/1");
        public DataSensor? MemoryUsedSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/0");
        public DataSensor? VirtualMemoryUsageSensor => Computer.SensorIndex.GetValueOrDefault("/ram/load/1");
        public DataSensor? VirtualMemoryAvailableSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/3");
        public DataSensor? VirtualMemoryUsedSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/2");

        // Network
        public string? NetworkName => Computer.HardwareList
            .FirstOrDefault(h => h.HardwareType == HardwareType.Network)?.Name;
        public DataSensor? NetworkUploadSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/nic/0/throughput/0");
        public DataSensor? NetworkDownloadSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/nic/0/throughput/1");
        public DataSensor? NetworkDataUploadedSensor => Computer.SensorIndex.GetValueOrDefault("/nic/0/data/0");
        public DataSensor? NetworkDataDownloadedSensor => Computer.SensorIndex.GetValueOrDefault("/nic/0/data/1");

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

            _monitorService.DataUpdated += () =>
            {
                CalculateEffectiveCpuClock();

                // Only notify computed/derived properties — sensor .Value bindings update automatically
                // via DataSensor's own [ObservableProperty] on Value.
                OnPropertyChanged(nameof(CpuName));
                OnPropertyChanged(nameof(GpuName));
                OnPropertyChanged(nameof(MemoryName));
                OnPropertyChanged(nameof(NetworkName));
            };
        }
    }
}