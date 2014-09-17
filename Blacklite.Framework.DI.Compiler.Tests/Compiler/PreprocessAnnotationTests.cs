using Blacklite.Framework.DI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using System;
using System.Reflection;
using System.Linq;
using Xunit;

namespace Blacklite.Framework.DI.Compiler.Tests
{

    public class PreprocessAnnotationTests
    {
        private CSharpCompilation GetCompilation()
        {
            return CSharpCompilation.Create("PreprocessAnnotationTests.dll",
                references: new[] {
                // This isn't very nice...
                new MetadataImageReference(System.IO.File.OpenRead(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll"))
                })
                // This way we don't have to reference anything but mscorlib.
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                using System;

                public enum LifecycleKind {
                    Singleton,
                    Scoped,
	                Transient
                }

                [AttributeUsage(AttributeTargets.Class)]
                public class ImplementationOfAttribute : Attribute
                {
                    public ImplementationOfAttribute(Type serviceType, LifecycleKind lifecycle = LifecycleKind.Transient)
                    {
                        ServiceType = serviceType;
                        Lifecycle = lifecycle;
                    }

                    public Type ServiceType { get; private set; }
                    public LifecycleKind Lifecycle { get; private set; }
                }"));
        }

        [Fact]
        public void Compiles()
        {
            var compilation = GetCompilation();

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void DefaultsToTransient()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA))]
                public class ProviderA : IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            collection.AddTransient(typeof (IProviderA), typeof (ProviderA));
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }


        [Fact]
        public void UnderstandsTransient()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA), LifecycleKind.Transient)]
                public class ProviderA : IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            collection.AddTransient(typeof (IProviderA), typeof (ProviderA));
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }


        [Fact]
        public void UnderstandsScoped()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA), LifecycleKind.Scoped)]
                public class ProviderA : IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            collection.AddScoped(typeof (IProviderA), typeof (ProviderA));
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void UnderstandsSingleton()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA), LifecycleKind.Singleton)]
                public class ProviderA : IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            collection.AddSingleton(typeof (IProviderA), typeof (ProviderA));
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void UnderstandsEverything()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA), LifecycleKind.Singleton)]
                public class ProviderA : IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"))
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderB1
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderB1))]
                public class ProviderB1 : IProviderB1
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"))
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderB
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderB), LifecycleKind.Transient)]
                public class ProviderB : IProviderB
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"))
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderC
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderC), LifecycleKind.Scoped)]
                public class ProviderC : IProviderC
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));

            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.Equal(@"using Microsoft.Framework.DependencyInjection;

namespace __generated
{
    public static class ServicesContainerExtensions
    {
        public static IServiceCollection AddImplementations(IServiceCollection collection)
        {
            collection.AddSingleton(typeof (IProviderA), typeof (ProviderA));
            collection.AddTransient(typeof (IProviderB1), typeof (ProviderB1));
            collection.AddTransient(typeof (IProviderB), typeof (ProviderB));
            collection.AddScoped(typeof (IProviderC), typeof (ProviderC));
            return collection;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }


        [Fact]
        public void ReportsDiagnostics()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                public interface IProviderB
                {
                    decimal GetValue();
                }

                [ImplementationOf(typeof(IProviderA), LifecycleKind.Singleton)]
                public class ProviderA : IProviderB
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }
                }"));


            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            Assert.True(context.Diagnostics.Any());
        }
    }
}