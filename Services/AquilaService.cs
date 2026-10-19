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

    /// <summary>The hardware read in flight on the thread pool, if any — see <see cref="OnTick"/>.</summary>
    private Task? _reading;

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
    /// The timer runs either way. LHMDriver's Read and Apply already return immediately when nothing is open,
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

        // The first poll on this thread, whole, before anything else looks: the sensor explorer reads
        // LibreHardwareMonitor's tree right after Start returns, and must find it read, not half-way.
        try
        {
            _driver.Read();
            Publish();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hardware poll");
        }

        _timer.Start();
    }

    public void SetInterval(int milliseconds)
        => _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>
    /// Announces every reading again, unchanged.
    ///
    /// A judged colour is worked out when a reading CHANGES — that is when a binding converts — so a new
    /// preset or new limits, which change the answer without changing the reading, reached only the readings
    /// that happened to move. A drive holding 38 °C kept the old preset's colour, or the old limit's verdict,
    /// until it read 39. The title bar re-raised for itself; every card binding waited.
    ///
    /// Not done by repainting the brushes in place instead: the charts turn a brush into a Skia colour when
    /// their brush PROPERTY changes, and a brush changed in place never changes the property.
    /// </summary>
    public void Reannounce()
    {
        foreach (var component in SensorCatalog.GetComponents(_state.Hardware))
            foreach (var entry in component.Sensors)
                entry.Sensor.Announce();
    }

    /// <summary>How often a reading arrives. A chart keeps a number of READINGS, so this is what turns one
    /// into a length of time — Settings lets the user change it, and nothing may assume it is a second.</summary>
    public TimeSpan Interval => _timer.Interval;

    /// <summary>
    /// A poll in two halves. Reading the hardware is the slow one — LibreHardwareMonitor's walk measured 36–43
    /// ms of a ~45 ms tick — and it touches nothing of ours, so it runs on the thread pool; done on the UI
    /// thread, as it was, it froze drawing, dragging and animation for two or three frames every tick. Writing
    /// the readings into the state stays here, on the UI thread, so every SensorNode change is still raised
    /// where it always was and no control has to marshal anything. Measured 2026-10-19 over 100 polls: the read
    /// 39–46 ms on average (up to 78) off the UI thread, and 4.1–4.8 ms left on it.
    ///
    /// A read still going when the next tick comes — a slow drive, a short interval — skips that tick rather
    /// than queueing behind it: two walks of the same hardware at once is not something LibreHardwareMonitor
    /// was ever asked to do.
    /// </summary>
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_reading is not null) return;

        try
        {
            _reading = Task.Run(_driver.Read);
            await _reading;
            if (_disposed) return;

            Publish();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hardware poll");
        }
        finally
        {
            _reading = null;
        }
    }

    /// <summary>The UI-thread half of a poll: the last read into the state, then everything that judges it.</summary>
    private void Publish()
    {
        _driver.Apply(_state);

        // Before the pressure is worked out, so the first judged tick already uses whatever limits the
        // drives and DIMMs report about themselves rather than our generic ones. Does its work once.
        vitals.AdoptReportedLimits(_state.Hardware);

        // Before the event: subscribers read Pressure in the same handler that reads the sensors,
        // and must not see last tick's value beside this tick's numbers.
        Pressure = _pressure.Evaluate(_state.Hardware);
        DataUpdated?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;

        // A read may be in flight on the thread pool, and closing LibreHardwareMonitor under it would pull its
        // objects away mid-walk. A read takes tens of milliseconds, so waiting for it costs nothing.
        try { _reading?.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* its failure was the poll's to report */ }

        _driver.Shutdown();
    }
}