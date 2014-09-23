using System;

namespace Blacklite.Framework.DI.Tests.Fixtures
{
    public interface IProviderB
    {
        int ItemA { get; }
    }

    public interface IProviderC
    {
        decimal ItemB { get; }
    }

    [ServiceDescriptor]
    public class ProviderBC : IProviderB, IProviderC
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