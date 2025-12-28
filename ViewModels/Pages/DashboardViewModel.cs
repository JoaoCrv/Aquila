using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService;

        public ComputerData Computer => _monitorService.ComputerData;
        public string? CpuName => _monitorService.ComputerData.HardwareList
                                            .FirstOrDefault(h => h.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu)?
                                            .Name;
        public DataSensor? CpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? CpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/load/0");
        //public DataSensor? CpuSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0"); //não existe sensor direto para cpu speed
        public DataSensor? CpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/amdcpu/0/power/0");
        public DataSensor? CpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/0");
        public DataSensor? CpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/fan/1");

        public DataSensor? GpuUsageSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? GpuTemperatureSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? GpuSpeedSensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? GpuEnergySensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? GpuFanSpeed1Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");
        public DataSensor? GpuFanSpeed2Sensor => Computer.SensorIndex.GetValueOrDefault("/lpc/nct6687d/0/temperature/0");



        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

            
            _monitorService.DataUpdated += () =>
            {
                OnPropertyChanged(nameof(CpuName));
                OnPropertyChanged(nameof(CpuTemperatureSensor));
                OnPropertyChanged(nameof(CpuUsageSensor));
                OnPropertyChanged(nameof(CpuEnergySensor));
                OnPropertyChanged(nameof(CpuFanSpeed1Sensor));
                OnPropertyChanged(nameof(CpuFanSpeed2Sensor));

            };
        }
    }
}