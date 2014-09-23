using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DI.Tests.Fixtures 
{
    public interface IOpenProviderA<T>
    {
    }

    [ServiceDescriptor(typeof(IOpenProviderA<>), Lifecycle = LifecycleKind.Singleton)]
    public class OpenProviderA<T> : IOpenProviderA<T>
    {
    }
}