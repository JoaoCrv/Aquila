using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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

        // Calculate CPU Speed
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
                    {
                        maxEffectiveClock = effectiveClock;
                    }
                }
            }

            
            EffectiveCpuClock = maxEffectiveClock;
        }


        //CPU
        public string? CpuName => _monitorService.ComputerData.HardwareList
                                            .FirstOrDefault(h => h.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu)?
                                            .Name;
        public DataSensor? CpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? CpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/load/0");
        //public DataSensor? CpuSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0"); //não existe sensor direto para cpu speed
        public DataSensor? CpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/power/0");
        public DataSensor? CpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/0");
        public DataSensor? CpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/1");


        //GPU
        public string? GpuName => _monitorService.ComputerData.HardwareList
                                    .FirstOrDefault(h => h.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuAmd)?
                                    .Name;
        public DataSensor? GpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/load/0");
        public DataSensor? GpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/temperature/0");
        public DataSensor? GpuSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/clock/0");
        public DataSensor? GpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/power/3");
        public DataSensor? GpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/fan/0");
        public DataSensor? GpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/gpu-amd/5/0/fan/1");

        //RAM
        public string? MemoryName => _monitorService.ComputerData.HardwareList
                                    .FirstOrDefault(h => h.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Memory)?
                                    .Name;
        public DataSensor? MemoryUsageSensor => Computer.SensorIndex.GetValueOrDefault("/ram/load/0");
        public DataSensor? MemoryAvailableSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/1");
        public DataSensor? MemoryUsedSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/0");
        public DataSensor? VirtualMemoryUsageSensor => Computer.SensorIndex.GetValueOrDefault("/ram/load/1");
        public DataSensor? VirtualMemoryAvailableSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/3");
        public DataSensor? VirtualMemoryUsedSensor => Computer.SensorIndex.GetValueOrDefault("/ram/data/2");

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

            
            _monitorService.DataUpdated += () =>
            {
                CalculateEffectiveCpuClock();
                //CPU
                OnPropertyChanged(nameof(CpuName));
                OnPropertyChanged(nameof(CpuTemperatureSensor));
                OnPropertyChanged(nameof(CpuUsageSensor));
                OnPropertyChanged(nameof(CpuEnergySensor));
                OnPropertyChanged(nameof(CpuFanSpeed1Sensor));
                OnPropertyChanged(nameof(CpuFanSpeed2Sensor));
                OnPropertyChanged(nameof(_effectiveCpuClock));

                //Gpu
                OnPropertyChanged(nameof(GpuName));
                OnPropertyChanged(nameof(GpuTemperatureSensor));
                OnPropertyChanged(nameof(GpuUsageSensor));
                OnPropertyChanged(nameof(GpuEnergySensor));
                OnPropertyChanged(nameof(GpuFanSpeed1Sensor));
                OnPropertyChanged(nameof(GpuFanSpeed2Sensor));

                //RAM
                OnPropertyChanged(nameof(MemoryName));
                OnPropertyChanged(nameof(MemoryUsageSensor));
                OnPropertyChanged(nameof(MemoryAvailableSensor));
                OnPropertyChanged(nameof(MemoryUsedSensor));
                OnPropertyChanged(nameof(VirtualMemoryUsageSensor));
                OnPropertyChanged(nameof(VirtualMemoryAvailableSensor));
                OnPropertyChanged(nameof(VirtualMemoryUsedSensor));


            };
        }
    }
}