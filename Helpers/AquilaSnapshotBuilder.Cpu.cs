using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // CPU

        private static SensorReading? CpuTemperature(HardwareState data) =>
            Find(data, HardwareType.Cpu, SensorType.Temperature, "Package")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Temperature);

        private static SensorReading? CpuLoad(HardwareState data) =>
            Find(data, HardwareType.Cpu, SensorType.Load, "CPU Total")
            ?? Find(data, HardwareType.Cpu, SensorType.Load, "Total")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Load);

        private static SensorReading? CpuPower(HardwareState data) =>
            Find(data, HardwareType.Cpu, SensorType.Power, "Package")
            ?? Find(data, HardwareType.Cpu, SensorType.Power, "Total")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Power);

        private static string? CpuSummary(HardwareState data)
        {
            var cpu = FirstHardware(data, HardwareType.Cpu);
            if (cpu == null) return null;

            int cores = cpu.Sensors
                .Count(s => s.SensorType == SensorType.Clock
                         && s.Name.Contains("Core #")
                         && !s.Name.Contains("("));
            if (cores == 0) return null;

            int threads = cpu.Sensors
                .Count(s => s.SensorType == SensorType.Load && s.Name.Contains("Thread #"));

            return threads > 0 ? $"{cores}C / {threads}T" : $"{cores} Cores";
        }

        private static float CpuEffectiveClock(HardwareState data)
        {
            var cpu = FirstHardware(data, HardwareType.Cpu);
            if (cpu == null)
                return 0;

            var clockSensors = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core #"))
                .ToList();
            var loadSensors = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core #"))
                .ToList();

            float maxEffectiveClock = 0;
            foreach (var clockSensor in clockSensors)
            {
                var coreNum = clockSensor.Name.Replace("Core #", string.Empty).Trim();
                var loadSensor = loadSensors.FirstOrDefault(s => s.Name.EndsWith(coreNum, StringComparison.Ordinal));
                float effectiveClock = loadSensor != null
                    ? clockSensor.Value * (loadSensor.Value / 100f)
                    : clockSensor.Value;

                if (effectiveClock > maxEffectiveClock)
                    maxEffectiveClock = effectiveClock;
            }

            return maxEffectiveClock;
        }

        private static List<SensorReading> CpuCoreSensors(HardwareState data)
        {
            var hw = FirstHardware(data, HardwareType.Cpu);
            if (hw == null) return [];
            return hw.Sensors
                .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core #"))
                .OrderBy(s => s.Index)
                .ToList();
        }

        private static SensorReading? CpuFan(HardwareState data, int index = 0) =>
            IndexedSensor(FirstHardware(data, HardwareType.Motherboard), SensorType.Fan, index);

        private static CpuSnapshot BuildCpuSnapshot(HardwareState data)
        {
            var cpu = FirstHardware(data, HardwareType.Cpu);

            return new CpuSnapshot
            {
                Name = cpu?.Name,
                Summary = CpuSummary(data),
                Temperature = MetricValue.FromSensor(CpuTemperature(data)),
                Load = MetricValue.FromSensor(CpuLoad(data)),
                EffectiveClock = MetricValue.FromValue(CpuEffectiveClock(data), "MHz", "Aquila effective clock"),
                Power = MetricValue.FromSensor(CpuPower(data)),
                FanRpm = MetricValue.FromSensor(CpuFan(data, 0)),
                Fan2Rpm = MetricValue.FromSensor(CpuFan(data, 1)),
                Cores = CpuCoreSensors(data)
                    .Select((sensor, index) => new CpuCoreSnapshot
                    {
                        Label = $"C{index + 1}",
                        Load = MetricValue.FromSensor(sensor)
                    })
                    .ToList()
            };
        }

    }
}
