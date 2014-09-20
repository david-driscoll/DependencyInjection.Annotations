using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Blacklite.Framework.DI
{
    public static class ServicesContainerExtensions
    {
        internal const string CompileTimeTypeName = "__generated.ServicesContainerExtensions";

        public static IServiceCollection AddFromAssembly(this IServiceCollection collection, object context, bool compile = true)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            
            return collection.AddFromAssembly(context.GetType(), compile);
        }

        public static IServiceCollection AddFromAssembly(this IServiceCollection collection, Type type, bool compile = true)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

#if ASPNETCORE50
            return collection.AddFromAssembly(type.GetTypeInfo().Assembly, compile);
#else
            return collection.AddFromAssembly(type.Assembly, compile);
#endif
        }

        public static IServiceCollection AddFromAssembly(this IServiceCollection collection, Assembly assembly, bool compile = true)
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

            return collection;
        }
    }
}