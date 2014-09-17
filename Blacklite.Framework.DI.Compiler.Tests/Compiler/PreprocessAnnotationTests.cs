using Blacklite.Framework.DI.Compiler;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Framework.Runtime;
using System;
using System.Linq;
using Xunit;

namespace Blacklite.Framework.DI.Compiler.Tests
{
    public class PreprocessAnnotationTests
    {
        [Fact]
        public void Compiles()
        {
            var compilation = CSharpCompilation.Create("PreprocessAnnotationTests");
            compilation = compilation.AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
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

                    public Type ServiceType { get; private set; };
                    public LifecycleKind Lifecycle { get; private set; };
                }"));

            compilation = compilation.AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(@"
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

    }
}