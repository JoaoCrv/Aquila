using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;

namespace Aquila.Services.LibreHardwareMonitor
{
    internal class LHMDriver : IHardwareDriver
    {
        private readonly ILogger<LHMDriver> _logger;
        private Computer? _computer;
        public IComputer? Computer => _computer;
        private LHMTranslater? _translater;
        private bool _hardwareLogged;

        public LHMDriver(ILogger<LHMDriver> logger) => _logger = logger;

        public string Name => "Libre Hardware Monitor";
        public string Version => "0.9.6";
        public bool RequiresElevation => true;
        public bool IsAvailable =>
            File.Exists("LibreHardwareMonitor.dll") &&
            new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

        public void Initialize()
        {
            _logger.LogInformation("Initializing LHM driver");
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsStorageEnabled = true,
                    IsNetworkEnabled = true
                };
                _computer.Open();
                _translater = new LHMTranslater();
                _logger.LogInformation("LHM driver initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LHM driver initialization failed");
                throw;
            }
        }

        public void Populate(AquilaState state)
        {
            if (_computer is null || _translater is null) return;

            _computer.Accept(new UpdateVisitor());

            if (!_hardwareLogged)
            {
                _hardwareLogged = true;
                LogHardwareStructure(_computer.Hardware);
            }

            _translater.Translate(_computer.Hardware, state);
        }

        private void LogHardwareStructure(IEnumerable<IHardware> hardware)
        {
            foreach (var hw in hardware)
            {
                _logger.LogDebug("[{Type}] {Name}", hw.HardwareType, hw.Name);
                foreach (var sensor in hw.Sensors)
                    _logger.LogDebug("  [{SensorType}] {SensorName}", sensor.SensorType, sensor.Name);
                foreach (var sub in hw.SubHardware)
                {
                    _logger.LogDebug("  Sub [{Type}] {Name}", sub.HardwareType, sub.Name);
                    foreach (var sensor in sub.Sensors)
                        _logger.LogDebug("    [{SensorType}] {SensorName}", sensor.SensorType, sensor.Name);
                }
            }
        }

        public void Shutdown()
        {
            _computer?.Close();
            _computer = null;
            _translater = null;
        }
    }
}
