using Aquila.Models;
using Aquila.Services.LibreHardwareMonitor;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using System;
using System.Windows.Threading;

namespace Aquila.Services;

public class AquilaService(IHardwareDriver driver, AquilaState state, VitalMonitor vitals,
    NoticeService notices, ILogger<AquilaService> logger) : IDisposable
{
    private readonly IHardwareDriver _driver = driver;
    private readonly AquilaState _state = state;
    private readonly NoticeService _notices = notices;
    private readonly ILogger<AquilaService> _logger = logger;
    /// <summary>A DispatcherTimer, so every reading is written — and every SensorNode change raised — on
    /// the UI thread. That is why no control marshals its own: seven did, each guarding against a thread
    /// that never called. A source that pushes from another thread (#12) marshals where it WRITES, not
    /// in every piece that draws.</summary>
    private readonly DispatcherTimer _timer = new();
    private bool _disposed;

    private readonly SystemPressure _pressure = new(vitals);

    public AquilaState State => _state;

    /// <summary>
    /// How hard the machine is being pushed, recomputed each tick.
    ///
    /// A derived value, published by the service rather than stored on the nodes — the same split as an
    /// API's computed field over its table. <see cref="AquilaState"/> stays a faithful record of what the
    /// hardware reported; anything that interprets those numbers belongs out here, where a second reading
    /// with different judgement can exist alongside this one.
    /// </summary>
    public PressureReading Pressure { get; private set; }

    public event Action? DataUpdated;

    // TODO: replace with IHardwareDriver.RawTree when multi-driver support is added
    public IComputer? Computer => (_driver as LHMDriver)?.Computer;

    /// <summary>
    /// Starts polling, WHETHER OR NOT the driver came up.
    ///
    /// It used to let the failure through and the application died with it — a source that would not open
    /// took the whole program, and what the user got was a crash rather than an explanation. That is the
    /// wrong shape for two reasons. The small one is that a dead application has nowhere to say anything,
    /// so the notice panel could never report the one failure it most needed to. The large one is that a
    /// driver is a SOURCE, and the point of #12 is that there will be several: Aquila must be able to run
    /// with one of them down, and eventually with LHM absent altogether.
    ///
    /// So the window opens, every reading is blank, and a condition says why. The blankness is then
    /// explained rather than mysterious, which is the same argument as the elevation notice carried to the
    /// case where elevation is not the cause.
    ///
    /// The driver still THROWS. It is right that it does — it is reporting that it could not start, and
    /// deciding what that means for the application is not its business. This is where that decision
    /// belongs, and it is the only place that has to change when there is more than one source to lose.
    ///
    /// The timer runs either way. LHMDriver.Populate already returns immediately when it has nothing open,
    /// so a tick costs nothing — and when a second source exists, a tick that stopped would take the
    /// working sources down with the broken one.
    /// </summary>
    public void Start()
    {
        _logger.LogInformation("Hardware monitor starting");

        try
        {
            _driver.Initialize();
            _notices.Clear("driver");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hardware source did not start; continuing without it");

            _notices.Set(
                "driver",
                "No hardware readings",
                "The monitoring driver did not start, so nothing can be read from this machine. "
                + $"Restarting Aquila as administrator is the usual fix. ({ex.Message})",
                Models.StatusKind.Bad);
        }

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

            // Before the pressure is worked out, so the first judged tick already uses whatever limits the
            // drives and DIMMs report about themselves rather than our generic ones. Does its work once.
            vitals.AdoptReportedLimits(_state.Hardware);

            // Before the event: subscribers read Pressure in the same handler that reads the sensors,
            // and must not see last tick's value beside this tick's numbers.
            Pressure = _pressure.Evaluate(_state.Hardware);
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