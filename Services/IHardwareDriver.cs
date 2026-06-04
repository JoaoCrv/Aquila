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
    void Populate(AquilaState state);
    void Shutdown();
}
