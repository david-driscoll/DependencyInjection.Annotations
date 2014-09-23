using System;
using Microsoft.Framework.DependencyInjection;

namespace Blacklite.Framework.DI.Tests.Fixtures
{
    public interface IService2
    {
        int Value { get; }
    }

    [ServiceDescriptor(typeof(IService2), Lifecycle = LifecycleKind.Scoped)]
    class Service2 : IService2
    {
        public int Value
        {
            get
            {
                return 9001;
            }
        }
    }
}