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
                        public ImplementationOfAttribute(Type serviceType = null, LifecycleKind lifecycle = LifecycleKind.Transient)
                        {
                            ServiceType = serviceType;
                            Lifecycle = lifecycle;
                        }

                        public Type ServiceType { get; set; }
                        public LifecycleKind Lifecycle { get; set; }
                    }

                    public interface IServiceCollection {}

                    public static class ServiceCollectionExtensions {
                        public static IServiceCollection AddFromAssembly(this IServiceCollection collection, Type type, bool compile = true) {
                            return collection;
                        }
                    }"))
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                    namespace Temp {
                        public class Startup {
                            void Configure(IServiceCollection services) {
                                services.AddFromAssembly(typeof(Startup), true);
                            }

                            public static int Main() { return 0; }
                        }
                    }"));
                //.AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(
                    //System.IO.File.ReadAllText(@"..\Blacklite.Framework.DI\Extensions\ServicesContainerExtensions.cs")));
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddTransient(typeof (IProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddTransient(typeof (IProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddScoped(typeof (IProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddSingleton(typeof (IProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddSingleton(typeof (IProviderA), typeof (ProviderA));
            services.AddTransient(typeof (IProviderB1), typeof (ProviderB1));
            services.AddTransient(typeof (IProviderB), typeof (ProviderB));
            services.AddScoped(typeof (IProviderC), typeof (ProviderC));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.True(context.Diagnostics.Any());
        }

        [Fact]
        public void AddFromAssemblyWorksWithThisExpression()
        {
            var compilation = GetCompilation();


            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void AddFromAssemblyIsLeftAloneForOtherTypes()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                    namespace Temp {
                        public class NotStartup {
                        }

                        public class Startup2 {
                            void Configure(IServiceCollection collection) {
                                collection.AddFromAssembly(typeof(NotStartup), true);
                            }
                        }
                    }"));


            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());

            Assert.Equal(@"
                    namespace Temp {
                        public class NotStartup {
                        }

                        public class Startup2 {
                            void Configure(IServiceCollection collection) {
                                collection.AddFromAssembly(typeof(NotStartup), true);
                            }
                        }
                    }", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void AddFromAssemblyIsLeftAloneWhenToldTo()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                    namespace Temp {
                        public class Startup2 {
                            void Configure(IServiceCollection collection) {
                                collection.AddFromAssembly(typeof(Startup2), false);
                            }
                        }
                    }"));


            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());

            Assert.Equal(@"
                    namespace Temp {
                        public class Startup2 {
                            void Configure(IServiceCollection collection) {
                                collection.AddFromAssembly(typeof(Startup2), false);
                            }
                        }
                    }", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void AddFromAssemblyIsReplacedByDefault()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                    namespace Temp {
                        public class Startup2 {
                            void Configure(IServiceCollection collection) {
                                collection.AddFromAssembly(typeof(Startup2));
                            }
                        }
                    }"));


            var context = new BeforeCompileContext()
            {
                CSharpCompilation = compilation
            };

            var unit = new PreprocessAnnotation();

            unit.BeforeCompile((IBeforeCompileContext)context);

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup2
    {
        void Configure(IServiceCollection collection)
        {
        }
    }
}", context.CSharpCompilation.SyntaxTrees.Last().GetText().ToString());
        }

        [Fact]
        public void DiagnosticsStillFunctionEvenIfReplacementNeverHappens()
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

            //Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.True(context.Diagnostics.Any());
        }

        [Fact]
        public void RegistersAllInterfacesIfServiceTypeIsNotDefined()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                public interface IProviderB
                {
                    decimal GetValue2();
                }

                [ImplementationOf( Lifecycle = LifecycleKind.Singleton )]
                class ProviderA : IProviderB, IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }

                    public decimal GetValue2()
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

            Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddTransient(typeof (IProviderB), typeof (ProviderA));
            services.AddTransient(typeof (IProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
        }

        [Fact]
        public void RegistersAllInterfacesAndTheClassIfTheClassIsPublicIfServiceTypeIsNotDefined()
        {
            var compilation = GetCompilation()
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
                public interface IProviderA
                {
                    decimal GetValue();
                }

                public interface IProviderB
                {
                    decimal GetValue2();
                }

                [ImplementationOf( Lifecycle = LifecycleKind.Singleton )]
                public class ProviderA : IProviderB, IProviderA
                {
                    public decimal GetValue()
                    {
                        return 9000.99M;
                    }

                    public decimal GetValue2()
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

            Console.WriteLine(context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());

            Assert.Equal(@"namespace Temp
{
    public class Startup
    {
        void Configure(IServiceCollection services)
        {
            services.AddTransient(typeof (IProviderB), typeof (ProviderA));
            services.AddTransient(typeof (IProviderA), typeof (ProviderA));
            services.AddTransient(typeof (ProviderA), typeof (ProviderA));
        }

        public static int Main()
        {
            return 0;
        }
    }
}", context.CSharpCompilation.SyntaxTrees.First(x => x.GetText().ToString().Contains("class Startup")).GetText().ToString());
        }
    }
}