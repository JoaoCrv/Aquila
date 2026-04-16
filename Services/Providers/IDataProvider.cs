using System;

namespace Aquila.Services.Providers
{
    public interface IDataProvider : IDisposable
    {
        void Initialize();
    }
}
