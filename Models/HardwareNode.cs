using Aquila.Models.Nodes;
using System.Collections.Generic;

namespace Aquila.Models;

public class HardwareNode
{
    public MotherboardNode Motherboard { get; } = new();
    public List<CpuNode> Cpus { get; } = new();
    public MemoryNode Memory { get; } = new();
    public List<GpuNode> Gpus { get; } = new();
    public List<NetworkNode> Networks { get; } = new();
    public List<StorageNode> Storages { get; } = new();

    public SensorNode TotalPower { get; } = new();

    /// <summary>
    /// The graphics card that matters — the one a single "GPU" reading should be about.
    ///
    /// Chosen by dedicated memory, because that is what separates a discrete card from an integrated
    /// one on every machine that has both: the integrated part reports a few hundred megabytes it
    /// borrowed, the card reports its own gigabytes. <c>Gpus[0]</c> is not that GPU — on a Ryzen with
    /// integrated graphics it is the idle one, which read 0% while the card beside it worked at 86%.
    ///
    /// A derivation, not an interpretation: it picks an existing node, it does not invent a value. Two
    /// discrete cards simply give the larger one, which is the best answer available without asking.
    /// </summary>
    public GpuNode? PrimaryGpu
    {
        get
        {
            GpuNode? best = null;
            var most = float.MinValue;

            foreach (var gpu in Gpus)
            {
                var vram = gpu.Data.Total.Value ?? 0;
                if (best is not null && vram <= most) continue;
                best = gpu;
                most = vram;
            }

            return best;
        }
    }
}