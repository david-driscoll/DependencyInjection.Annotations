using Microsoft.Framework.DependencyInjection;
using System;

namespace Blacklite.Framework.DependencyInjection.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ImplementationOfAttribute(Type serviceType, LifecycleKind lifecycle = LifecycleKind.Transient) : Attribute
    {
        public Type ServiceType { get; } = serviceType;
        public LifecycleKind Lifecycle { get; } = lifecycle;
    }
}