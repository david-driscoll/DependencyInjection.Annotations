using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DependencyInjection.Annotations.Tests.Web
{
    public interface IProviderA
    {
        decimal GetValue();
    }

    [ImplementationOf(typeof(IProviderA), LifecycleKind.Singleton)]
    public class ProviderA : IProviderA
    {
        public decimal GetValue()
        {
            return 9000.99M;
        }
    }
}