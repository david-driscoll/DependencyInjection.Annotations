using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.Framework.Runtime;

namespace Blacklite.Framework.DI.Compiler
{
    public class Container<TDeclaration, TSymbol>(TDeclaration declaration, TSymbol symbol)
    {
        public TDeclaration Declaration { get; } = declaration;
        public TSymbol Symbol { get; } = symbol;
    }

    public class PreprocessAnnotation : ICompileModule
    {
        private IEnumerable<Container<ClassDeclarationSyntax, INamedTypeSymbol>> GetClassesWithImplementationAttribute(SyntaxTree syntaxTree, SemanticModel model)
        {
            // Find all classes that have our attribute.
            var classesWithAttribute = syntaxTree
                       .GetRoot()
                       .DescendantNodes()
                       .OfType<ClassDeclarationSyntax>()
                       .Select(declaration => new Container<ClassDeclarationSyntax, INamedTypeSymbol>(declaration, model.GetDeclaredSymbol(declaration)))
                       .Where(x => x.Symbol.GetAttributes()
                           .Any(z => z.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute")));

            // Each return the container for each class
            foreach (var container in classesWithAttribute)
            {
                yield return container;
            }
        }

        private NameSyntax BuildQualifiedName(string type)
        {
            // Create a qualified name for every piece of the type.
            // If you don't do this... bad things happen.
            NameSyntax nameSyntax;
            var parts = type.Split('.');

            var identifiers = parts.Select(SyntaxFactory.IdentifierName);
            if (identifiers.Count() > 1)
            {
                QualifiedNameSyntax qns = SyntaxFactory.QualifiedName(
                    identifiers.First(),
                    identifiers.Skip(1).First()
                );

                foreach (var usingId in identifiers.Skip(2))
                {
                    qns = SyntaxFactory.QualifiedName(qns, usingId);
                }

                nameSyntax = qns;
            }
            else
            {
                nameSyntax = identifiers.Single();
            }


            return nameSyntax;
        }

        public IEnumerable<StatementSyntax> GetStatements(IBeforeCompileContext context, IEnumerable<Container<ClassDeclarationSyntax, INamedTypeSymbol>> containers)
        {
            foreach (var container in containers)
            {
                yield return GetStatement(context, container.Symbol, container.Declaration);
            }
        }

        public StatementSyntax GetStatement(IBeforeCompileContext context, INamedTypeSymbol symbol, ClassDeclarationSyntax declaration)
        {
            var attributeSymbol = symbol.GetAttributes().Single(x => x.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute"));
            var attributeDeclaration = declaration.AttributeLists
                .SelectMany(z => z.Attributes)
                .Single(z => z.Name.ToString().Contains("ImplementationOf"));


            var serviceType = attributeSymbol.ConstructorArguments[0].Value.ToString();
            var serviceQualifiedName = BuildQualifiedName(serviceType);

            var implementationType = symbol.ToDisplayString();
            var implementationQualifiedName = BuildQualifiedName(implementationType);

            var baseTypes = new List<string>();
            var impType = symbol;
            while (impType != null)
            {
                baseTypes.Add(impType.ToDisplayString());
                impType = impType.BaseType;
            }

            // TODO: Enforce implementation is assignable to service
            // Diagnostic error?
            var potentialBaseTypes = baseTypes.Concat(
                symbol.AllInterfaces.Select(z => z.ToDisplayString())
            );

            // This is where some of the power comes out.
            // We now have the ability to throw compile time errors if we believe something is wrong.
            // This could be extended to support generic types, and potentially matching compatible open generic types together to build a list.
            if (!potentialBaseTypes.Any(z => serviceType.Equals(z, StringComparison.OrdinalIgnoreCase)))
            {
                var serviceName = serviceType.Split('.').Last();
                var implementationName = implementationType.Split('.').Last();

                context.Diagnostics.Add(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DI0001",
                            "Implementation miss-match",
                            "The implementation '{0}' does not implement the service '{1}'",
                            "DependencyInjection",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        Location.Create(attributeDeclaration.SyntaxTree, attributeDeclaration.Span),
                        implementationName,
                        serviceName
                    )
                );
            }

            // Determine the lifecycle that was given.
            var lifecycle = GetLifecycle((int)attributeSymbol.ConstructorArguments[1].Value);

            // Build the Statement
            return GetCollectionExpressionStatement(lifecycle, serviceQualifiedName, implementationQualifiedName);
        }

        public string GetLifecycle(int enumValue)
        {
            string lifecycle;
            switch (enumValue)
            {
                case 1:
                    lifecycle = "Scoped";
                    break;
                case 0:
                    lifecycle = "Singleton";
                    break;
                default:
                    lifecycle = "Transient";
                    break;
            }

            return lifecycle;
        }

        public StatementSyntax GetCollectionExpressionStatement(string lifecycle, NameSyntax serviceQualifiedName, NameSyntax implementationQualifiedName)
        {
            // I hear there is a better way to do this... that will be released sometime.
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("collection"),
                        name: SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("Add" + lifecycle))
                    ),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(
                            new[] {
                                    SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(serviceQualifiedName)),
                                    SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(implementationQualifiedName))
                                })
                        )
                    )
                );
        }

        public void BeforeCompile(IBeforeCompileContext context)
        {
            // Find all the class containers we care about.
            var containers = context.CSharpCompilation.SyntaxTrees
                .Select(tree => new
                {
                    Model = context.CSharpCompilation.GetSemanticModel(tree),
                    SyntaxTree = tree,
                    Root = tree.GetRoot()
                })
                .SelectMany(ctx => GetClassesWithImplementationAttribute(ctx.SyntaxTree, ctx.Model));

            // Build the registration statements out of the containers.
            var nodes = GetStatements(context, containers);

            // Build our new extension method.
            // This baby can technically be called as an extension once vNext compiles this code
            // But currently the best way for (tooling) is to call this methid via reflection.
            var @method = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName("IServiceCollection"), "AddImplementations")
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        SyntaxFactory.Parameter(
                            default(SyntaxList<AttributeListSyntax>),
                            default(SyntaxTokenList),
                            SyntaxFactory.IdentifierName("IServiceCollection"),
                            SyntaxFactory.Identifier("collection"),
                            null
                        ).AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword))
                    )
                    // Add our statements
                    .AddBodyStatements(nodes.ToArray())
                    // Our method returns the IServiceCollection instance
                    .AddBodyStatements(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("collection")));

            // Build our extension class, it's static and public and contains our method
            var @class = SyntaxFactory.ClassDeclaration("ServicesContainerExtensions")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(@method);

            // Build a namespace to house our method in.
            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("__generated"))
                .AddMembers(@class)
                .NormalizeWhitespace();

            // Create a new compliation unit.
            // This references the Dependency injection namepsace for the IServiceCollection interface
            var newCompilationUnit = SyntaxFactory.CompilationUnit()
                .AddUsings(SyntaxFactory.UsingDirective(BuildQualifiedName("Microsoft.Framework.DependencyInjection")))
                .AddMembers(@namespace)
                .NormalizeWhitespace();

            // Generate the syntax tree
            var newSyntaxTree = SyntaxFactory.SyntaxTree(newCompilationUnit, context.CSharpCompilation.SyntaxTrees[0].Options);

            // Get a new C# compliation, with our new syntax tree
            var newCompilation = context.CSharpCompilation.AddSyntaxTrees(newSyntaxTree);

            // Replace the compliation with our new one.
            context.CSharpCompilation = newCompilation;
        }

        public void AfterCompile(IAfterCompileContext context)
        {
            // Not Used
        }
    }
}
