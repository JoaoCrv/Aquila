using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Services.LibreHardwareMonitor
{
    internal class LHMDriver : IHardwareDriver
    {
        
        private Computer? _computer;
        public IComputer? Computer => _computer;
        private LHMTranslater? _translater;

        public string Name => "Libre Hardware Monitor";
        public string Version => "0.9.4";
        public bool IsAvailable =>
        File.Exists("LibreHardwareMonitor.dll") &&
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        public void Initialize()
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
        }

        public void Populate(AquilaState state)
        {
            if (_computer is null || _translater is null) return;

            _computer.Accept(new UpdateVisitor());
            _translater.Translate(_computer.Hardware, state);
        }

        public void Shutdown()
        {
            _computer?.Close();
            _computer = null;
            _translater = null;
        }
    }
}
