using Aquila.Models;

namespace Aquila.Services;

public interface IHardwareDriver
{
    string Name { get; }
    string Version { get; }
    bool IsAvailable { get; }

    void Initialize();
    void Populate(AquilaState state);
    void Shutdown();
}
