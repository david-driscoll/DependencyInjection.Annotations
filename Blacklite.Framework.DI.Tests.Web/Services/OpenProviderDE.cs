using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DI.Tests.Web
{
    public interface IOpenProviderD<T>
    {
        T ItemA { get; }
    }

    public interface IOpenProviderE<T>
    {
        T ItemB { get; }
    }

    public interface IOpenProviderF<T>
    {
        T Value { get; }
    }

    [ServiceDescriptor(Lifecycle = LifecycleKind.Scoped)]
    class OpenProviderDE<T> : IOpenProviderD<T>, IOpenProviderE<T>, IOpenProviderF<string>
    {
        public T ItemA
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public T ItemB
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Value
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}