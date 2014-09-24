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

            var assembly = type.GetTypeInfo().Assembly;

            return collection.AddAssembly(assembly, compile);
        }

        private static IEnumerable<Type> GetAllBaseTypes(Type type)
        {
            while (type.GetTypeInfo().BaseType != null)
            {
                yield return type;
                type = type.GetTypeInfo().BaseType;
            }
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
                    ImplementationType = x,
                    Attribute = x.GetTypeInfo().GetCustomAttribute<ServiceDescriptorAttribute>(true)
                })
                .Where(x => x.Attribute != null);

            foreach (var service in services)
            {
                var implementationType = service.ImplementationType;
                IEnumerable<Type> serviceTypes = null;
                if (service.Attribute.ServiceType == null)
                {
                    serviceTypes = implementationType.GetInterfaces();
                    if (implementationType.GetTypeInfo().IsPublic)
                    {
                        // TODO:  Should this include all base types?  Should it be the lowest base type (HttpContext for example)? 
                        serviceTypes = serviceTypes.Concat(new[] { implementationType });
                    }

                    if (implementationType.GetTypeInfo().ContainsGenericParameters)
                    {
                        var parameters = implementationType.GetGenericArguments();

                        serviceTypes = serviceTypes.Where(type => parameters
                            .Join(type.GetGenericArguments(), x => x.Name, x => x.Name, (x, y) => true).Count() == parameters.Count())
                            .Select(x => x.GetGenericTypeDefinition());
                    }
                }
                else
                {
                    if (service.Attribute.ServiceType.GetTypeInfo().IsGenericTypeDefinition)
                    {
                        implementationType = implementationType.GetGenericTypeDefinition();
                    }
                    serviceTypes = new[] { service.Attribute.ServiceType };
                }

                foreach (var serviceType in serviceTypes)
                {
                    if (service.Attribute.ServiceType == null || // We'ere registering everything, and we've already filtered inapplicable types
                        serviceType.IsAssignableFrom(implementationType) || // Handle the most basic registration
                        service.ImplementationType.GetInterfaces() // Handle the open implementation....
                            .Concat(GetAllBaseTypes(service.ImplementationType))
                            .Select(z => z.GetGenericTypeDefinition())
                            .Any(z => z == serviceType)
                        )
                    {
                        var lifecycle = service.Attribute.Lifecycle;

                        collection.Add(new ServiceDescriptor()
                        {
                            ImplementationType = implementationType,
                            ServiceType = serviceType,
                            Lifecycle = service.Attribute.Lifecycle
                        });
                    }
                    else
                    {
                        throw new InvalidCastException(string.Format("Service Type '{0}' is not assignable from Implementation Type '{1}'.",
                            serviceType.FullName,
                            implementationType.FullName)
                        );
                    }
                }
            }

            return collection;
        }
    }
}