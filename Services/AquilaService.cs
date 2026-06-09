using Aquila.Models;
using Aquila.Services.LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using System;
using System.Windows.Threading;

namespace Aquila.Services;

public class AquilaService(IHardwareDriver driver, AquilaState state, ILogger<AquilaService> logger) : IDisposable
{
    private readonly IHardwareDriver _driver = driver;
    private readonly AquilaState _state = state;
    private readonly ILogger<AquilaService> _logger = logger;
    private readonly DispatcherTimer _timer = new();
    private bool _disposed;

    public AquilaState State => _state;
    public event Action? DataUpdated;

    // TODO: replace with IHardwareDriver.RawTree when multi-driver support is added
    public IComputer? Computer => (_driver as LHMDriver)?.Computer;

    public void Start()
    {
        _logger.LogInformation("Hardware monitor starting");
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
        try
        {
            _driver.Populate(_state);
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hardware poll");
        }
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