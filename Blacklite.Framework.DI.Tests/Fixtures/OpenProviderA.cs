using System;

namespace Blacklite.Framework.DI.Tests.Fixtures 
{
    public interface IOpenProviderA<T>
    {
    }

    [ServiceDescriptor(typeof(IOpenProviderA<>))]
    public class OpenProviderA<T> : IOpenProviderA<T>
    {
    }
}