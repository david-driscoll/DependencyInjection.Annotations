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

        private static IEnumerable<Type> GetAllBaseTypes(Type type)
        {
            while (type.BaseType != null)
            {
                yield return type;
                type = type.BaseType;
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
#if ASPNETCORE50
                    Attribute = x.GetTypeInfo().GetCustomAttribute<ServiceDescriptorAttribute>(true)
#else
                    Attribute = x.GetCustomAttribute<ServiceDescriptorAttribute>(true)
#endif
                })
                .Where(x => x.Attribute != null);

            foreach (var service in services)
            {
                var implementationType = service.ImplementationType;
                IEnumerable<Type> serviceTypes = null;
                if (service.Attribute.ServiceType == null)
                {
                    serviceTypes = implementationType.GetInterfaces();
                    if (implementationType.IsPublic)
                    {
                        // TODO:  Should this include all base types?  Should it be the lowest base type (HttpContext for example)? 
                        serviceTypes = serviceTypes.Concat(new[] { implementationType });
                    }

                    if (implementationType.ContainsGenericParameters)
                    {
                        var parameters = implementationType.GetGenericArguments();

                        serviceTypes = serviceTypes.Where(type => parameters
                            .Join(type.GetGenericArguments(), x => x.Name, x => x.Name, (x, y) => true).Count() == parameters.Count())
                            .Select(x => x.GetGenericTypeDefinition());
                    }
                }
                else
                {
                    if (service.Attribute.ServiceType.IsGenericTypeDefinition)
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

                        Console.WriteLine("{0}, {1}", implementationType.FullName, serviceType.FullName);

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
                Console.WriteLine("----------");

            }

            return collection;
        }
    }
}