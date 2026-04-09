using Aquila.Models.Api;
using System;

namespace Aquila.Services.Providers
{
    public interface IDataProvider : IDisposable
    {
        void Initialize();
        void Populate(AquilaState apiState);
    }
}
