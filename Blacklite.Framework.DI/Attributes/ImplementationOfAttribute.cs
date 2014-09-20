using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DI
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ImplementationOfAttribute(Type serviceType = null, LifecycleKind lifecycle = LifecycleKind.Transient) : Attribute
    {
        public Type ServiceType { get; set; } = serviceType;
        public LifecycleKind Lifecycle { get; set; } = lifecycle;
    }
}