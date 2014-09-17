﻿using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Blacklite.Framework.DependencyInjection.Annotations
{
    public static class ServicesContainerExtensions
    {
        internal const string CompileTimeTypeName = "__generated.ServicesContainerExtensions";

        public static IServiceCollection FindAllImplementations(this IServiceCollection collection, Type type)
        {
#if ASPNETCORE50
            return collection.FindAllImplementations(type.GetTypeInfo().Assembly);
#else
            return collection.FindAllImplementations(type.Assembly);
#endif
        }

        public static IServiceCollection FindAllImplementations(this IServiceCollection collection, Assembly assembly)
        {
            var generatedExtensions = assembly.GetType(CompileTimeTypeName);
            if (generatedExtensions != null)
            {
                // In theory the following call should work... but with current (I hope) tooling, the preprocess is not indentified properly.
                // __generated.ServicesContainerExtensions.AddImplementations(collection);
                generatedExtensions.GetMethod("AddImplementations").Invoke(null, new object[] { collection });
            }
            else
            {
                FindAllRuntimeImplementations(collection, assembly);
            }

            return collection;
        }

        private static void FindAllRuntimeImplementations(IServiceCollection collection, Assembly assembly)
        {
            var services = assembly.GetTypes()
            .Select(x => new
            {
                Type = x,
#if ASPNETCORE50
                    Attribute = x.GetTypeInfo().GetCustomAttribute<ImplementationOfAttribute>(true)
#else
                Attribute = x.GetCustomAttribute<ImplementationOfAttribute>(true)
#endif
            })
            .Where(x => x.Attribute != null);

            foreach (var service in services)
            {
                if (service.Attribute.ServiceType.IsAssignableFrom(service.Type))
                {
                    switch (service.Attribute.Lifecycle)
                    {
                        case LifecycleKind.Singleton:
                            collection.AddSingleton(service.Attribute.ServiceType, service.Type);
                            break;

                        case LifecycleKind.Scoped:
                            collection.AddScoped(service.Attribute.ServiceType, service.Type);
                            break;

                        case LifecycleKind.Transient:
                        default:
                            collection.AddTransient(service.Attribute.ServiceType, service.Type);
                            break;
                    }
                }
                else
                {
                    throw new InvalidCastException(string.Format("Service Type '{0}' is not assignable from Implementation Type '{1}'.",
                        service.Attribute.ServiceType.FullName,
                        service.Type.FullName));
                }
            }
        }
    }
}