using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models;

public class AquilaState
{
    public HardwareNode Hardware { get; } = new();
    public event Action? DataUpdated;
    public void Commit() => DataUpdated?.Invoke();
}