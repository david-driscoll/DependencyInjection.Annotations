using System;

namespace Blacklite.Framework.DI.Tests.Fixtures
{
    public interface IOpenProviderB<T>
    {
        int ItemA { get; }
    }

    public interface IOpenProviderC<T>
    {
        decimal ItemB { get; }
    }

    [ServiceDescriptor]
    public class OpenProviderBC<T> : IOpenProviderB<T>, IOpenProviderC<T>
    {
        public int ItemA
        {
            get
            {
                return 1234;
            }
        }

        public decimal ItemB
        {
            get
            {
                return 5678.90M;
            }
        }
    }
}