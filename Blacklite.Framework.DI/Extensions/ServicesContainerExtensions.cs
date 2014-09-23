using Microsoft.Framework.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Blacklite.Framework.DI
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddAssembly(this IServiceCollection collection, object context, bool compile = true)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            return collection.AddAssembly(context.GetType(), compile);
        }

        public static IServiceCollection AddAssembly(this IServiceCollection collection, Type type, bool compile = true)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

#if ASPNETCORE50
            var assembly = type.GetTypeInfo().Assembly;
#else
            var assembly = type.Assembly;
#endif

            return collection.AddAssembly(assembly, compile);
        }

        public static IServiceCollection AddAssembly(this IServiceCollection collection, Assembly assembly, bool compile = true)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            var services = assembly.GetTypes()
                .Select(x => new
                {
                    Type = x,
#if ASPNETCORE50
                    Attribute = x.GetTypeInfo().GetCustomAttribute<ServiceDescriptorAttribute>(true)
#else
                    Attribute = x.GetCustomAttribute<ServiceDescriptorAttribute>(true)
#endif
                })
                .Where(x => x.Attribute != null);

            foreach (var service in services)
            {
                IEnumerable<Type> serviceTypes = null;
                if (service.Attribute.ServiceType == null)
                {
                    serviceTypes = service.Type.GetInterfaces();
                    if (service.Type.IsPublic)
                    {
                        serviceTypes = serviceTypes.Concat(new[] { service.Type });
                    }
                }
                else
                {
                    serviceTypes = new[] { service.Attribute.ServiceType };
                }

                foreach (var serviceType in serviceTypes)
                {
                    if (serviceType.IsAssignableFrom(service.Type))
                    {
                        var lifecycle = service.Attribute.Lifecycle;

                        collection.Add(new ServiceDescriptor()
                        {
                            ImplementationType = service.Type,
                            ServiceType = serviceType,
                            Lifecycle = service.Attribute.Lifecycle
                        });
                    }
                    else
                    {
                        throw new InvalidCastException(string.Format("Service Type '{0}' is not assignable from Implementation Type '{1}'.",
                            serviceType.FullName,
                            service.Type.FullName)
                        );
                    }
                }
            }

            return collection;
        }
    }
}