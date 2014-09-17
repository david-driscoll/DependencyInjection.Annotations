using System;
using Microsoft.Framework.DependencyInjection;

namespace Blacklite.Framework.DI.Tests.Web
{
    public interface IService2
    {
        int Value { get; }
    }

    [ImplementationOf(typeof(IService2), LifecycleKind.Scoped)]
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