using Aquila.Models;
using Aquila.Services.LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Aquila.Services;

public class AquilaService(IHardwareDriver driver, AquilaState state) :IDisposable
{
    private readonly IHardwareDriver _driver = driver;
    private readonly AquilaState _state = state;
    private readonly DispatcherTimer _timer = new DispatcherTimer();
    private bool _disposed;

    public AquilaState State => _state;
    public event Action? DataUpdated;
    public IComputer? Computer => (_driver as LHMDriver)?.Computer;
    public void Start()
    {
        _driver.Initialize();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;

        // poll inicial imediato
        OnTick(null, EventArgs.Empty);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Console.WriteLine("Tick");
        _driver.Populate(_state);
        DataUpdated?.Invoke();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        _driver?.Shutdown();
    }
}