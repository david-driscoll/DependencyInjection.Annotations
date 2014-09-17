using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DependencyInjection.Annotations.Tests.Web
{
    public interface IService1
    {
        string Value { get; }
    }

    [ImplementationOf(typeof(IService1))]
    public class Service1 : IService1
    {
        public string Value
        {
            get
            {
                return "OVER 9000";
            }
        }
    }
}