using System;
using System.Reflection;
using System.Linq;
using Xunit;
using Blacklite.Framework.DI;
using Microsoft.Framework.DependencyInjection;
using System.Collections.Generic;
using Blacklite.Framework.DI.Tests.Fixtures;

namespace Blacklite.Framework.DI.Tests
{
    public class ServicesContainerExtensionsTests
    {
        [Fact]
        public void ServiceCollectionAcceptsObjectContext()
        {
            var collection = new ServiceCollection();

            collection.AddAssembly(this);

            Assert.Equal(collection.Count(), 8);
        }

        [Fact]
        public void ServiceCollectionAcceptsTypeContext()
        {
            var collection = new ServiceCollection();

            collection.AddAssembly(typeof(ServicesContainerExtensionsTests));

            Assert.Equal(collection.Count(), 8);
        }

        [Fact]
        public void ServiceCollectionAcceptsAssemblyContext()
        {
            var collection = new ServiceCollection();

#if ASPNETCORE50
            collection.AddAssembly(typeof(ServicesContainerExtensionsTests).GetTypeInfo().Assembly);
#else
            collection.AddAssembly(typeof(ServicesContainerExtensionsTests).Assembly);
#endif

            Assert.Equal(collection.Count(), 8);
        }

        private class ServiceDescriptorEqualityComparer : IEqualityComparer<IServiceDescriptor>
        {
            public bool Equals(IServiceDescriptor x, IServiceDescriptor y)
            {
                return x.ImplementationType == y.ImplementationType && x.Lifecycle == y.Lifecycle && x.ServiceType == y.ServiceType;
            }

            public int GetHashCode(IServiceDescriptor obj)
            {
                return obj.GetHashCode();
            }
        }

        public void ServiceCollectionContainsPublicClassesWhenUsedGenerically()
        {
            var collection = new ServiceCollection();
            var eqalityComparer = new ServiceDescriptorEqualityComparer();

            collection.AddAssembly(this);

            var descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IProviderB),
                ImplementationType = typeof(ProviderBC),
                Lifecycle = LifecycleKind.Transient
            };
                
            Assert.Contains(descriptor, collection, eqalityComparer);

            descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IProviderC),
                ImplementationType = typeof(ProviderBC),
                Lifecycle = LifecycleKind.Transient
            };

            Assert.Contains(descriptor, collection, eqalityComparer);

            descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(ProviderBC),
                ImplementationType = typeof(ProviderBC),
                Lifecycle = LifecycleKind.Transient
            };

            Assert.Contains(descriptor, collection, eqalityComparer);

            descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IProviderD),
                ImplementationType = typeof(ProviderDE),
                Lifecycle = LifecycleKind.Scoped
            };

            Assert.Contains(descriptor, collection, eqalityComparer);

            descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IProviderE),
                ImplementationType = typeof(ProviderDE),
                Lifecycle = LifecycleKind.Scoped
            };

            Assert.Contains(descriptor, collection, eqalityComparer);

            descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(ProviderDE),
                ImplementationType = typeof(ProviderDE),
                Lifecycle = LifecycleKind.Scoped
            };

            Assert.DoesNotContain(descriptor, collection, eqalityComparer);
        }

        [Fact]
        public void ServiceCollectionIncludesProviderA()
        {
            var collection = new ServiceCollection();
            var eqalityComparer = new ServiceDescriptorEqualityComparer();

            collection.AddAssembly(this);

            var descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IProviderA),
                ImplementationType = typeof(ProviderA),
                Lifecycle = LifecycleKind.Singleton
            };

            Assert.Contains(descriptor, collection, eqalityComparer);
        }

        [Fact]
        public void ServiceCollectionIncludesService1()
        {
            var collection = new ServiceCollection();
            var eqalityComparer = new ServiceDescriptorEqualityComparer();

            collection.AddAssembly(this);

            var descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IService1),
                ImplementationType = typeof(Service1),
                Lifecycle = LifecycleKind.Transient
            };

            Assert.Contains(descriptor, collection, eqalityComparer);
        }

        [Fact]
        public void ServiceCollectionIncludesService2()
        {
            var collection = new ServiceCollection();
            var eqalityComparer = new ServiceDescriptorEqualityComparer();

            collection.AddAssembly(this);

            var descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IService2),
                ImplementationType = typeof(Service2),
                Lifecycle = LifecycleKind.Scoped
            };

            Assert.Contains(descriptor, collection, eqalityComparer);
        }

        [Fact]
        public void ServiceCollectionIncludsOpenProviderA()
        {
            var collection = new ServiceCollection();
            var eqalityComparer = new ServiceDescriptorEqualityComparer();

            collection.AddAssembly(this);

            var descriptor = new ServiceDescriptor()
            {
                ServiceType = typeof(IOpenProviderA<>),
                ImplementationType = typeof(OpenProviderA<>),
                Lifecycle = LifecycleKind.Transient
            };

            Assert.Contains(descriptor, collection, eqalityComparer);
        }
    }
}