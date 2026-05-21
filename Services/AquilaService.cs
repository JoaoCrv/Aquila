using Aquila.Models;
using Aquila.Services.LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;
using System;
using System.Windows.Threading;

namespace Aquila.Services;

public class AquilaService(IHardwareDriver driver, AquilaState state) : IDisposable
{
    private readonly IHardwareDriver _driver = driver;
    private readonly AquilaState _state = state;
    private readonly DispatcherTimer _timer = new();
    private bool _disposed;

    public AquilaState State => _state;
    public event Action? DataUpdated;

    // TODO: replace with IHardwareDriver.RawTree when multi-driver support is added
    public IComputer? Computer => (_driver as LHMDriver)?.Computer;

    public void Start()
    {
        _driver.Initialize();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;
        OnTick(null, EventArgs.Empty);
        _timer.Start();
    }

    public void SetInterval(int milliseconds)
        => _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);

    private void OnTick(object? sender, EventArgs e)
    {
        _driver.Populate(_state);
        RecordHistory();
        DataUpdated?.Invoke();
    }

    private void RecordHistory()
    {
        var hw = _state.Hardware;
        foreach (var cpu in hw.Cpus)
        {
            cpu.Load.Total.Record();
            cpu.Temperature.Primary.Record();
        }
        hw.Memory.Load.Total.Record();
        hw.TotalPower.Record();
        hw.Motherboard.CpuFan?.Record();
        foreach (var net in hw.Networks)
        {
            net.Throughput.Download.Record();
            net.Throughput.Upload.Record();
        }
        foreach (var gpu in hw.Gpus)
            gpu.Load.Core.Record();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _driver.Shutdown();
    }
}