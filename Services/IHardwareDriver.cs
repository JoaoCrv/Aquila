using Aquila.Models;

namespace Aquila.Services;

public interface IHardwareDriver
{
    string Name { get; }
    string Version { get; }
    bool IsAvailable { get; }

    /// <summary>
    /// Whether this driver needs the process to run elevated to read all sensors. LHM needs it for
    /// the kernel driver (motherboard node: fans, temps, DIMM details); future API/SDK-based drivers
    /// may not. The app uses this to decide whether to elevate at startup.
    /// </summary>
    bool RequiresElevation { get; }

    void Initialize();

    /// <summary>
    /// Reads the hardware into the driver's own objects. May run on any thread — it is the slow half of a
    /// poll (LibreHardwareMonitor's walk measured 36–43 ms), so AquilaService runs it off the UI thread — and
    /// must therefore never touch <see cref="AquilaState"/>, whose nodes raise their changes on the UI thread.
    /// </summary>
    void Read();

    /// <summary>Writes the last <see cref="Read"/> into the state. Always on the UI thread, never during a read.</summary>
    void Apply(AquilaState state);

    void Shutdown();
}
