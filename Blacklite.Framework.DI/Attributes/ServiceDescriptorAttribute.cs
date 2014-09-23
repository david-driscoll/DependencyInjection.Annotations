using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DI
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceDescriptorAttribute(Type serviceType = null) : Attribute
    {
        public Type ServiceType { get; set; } = serviceType;
        public LifecycleKind Lifecycle { get; set; } = LifecycleKind.Transient;
    }
}