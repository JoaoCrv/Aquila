using Aquila.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Services
{
    public interface IHardwareDriver
    {
        string Name { get; }
        string Version { get; }
        bool IsAvailable { get; }

        void Initialize();
        void Populate(AquilaState state);

        void Shutdown();
    }
}
