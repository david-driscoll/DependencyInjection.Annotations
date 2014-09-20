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
    public class Container<TDeclaration, TSymbol>
    {
        public Container(TDeclaration declaration, TSymbol symbol) {
            Declaration = declaration;
            Symbol = symbol;
        }

        public TDeclaration Declaration { get; private set; }
        public TSymbol Symbol { get; private set; }
    }

    public class PreprocessAnnotation : ICompileModule
    {
        private IEnumerable<Container<InvocationExpressionSyntax, IMethodSymbol>> GetAddFromAssemblyMethodCall(SyntaxTree syntaxTree, SemanticModel model)
        {
            // Find all classes that have our attribute.
            var addFromAssemblyExpressions = syntaxTree
                       .GetRoot()
                       .DescendantNodes()
                       .OfType<MemberAccessExpressionSyntax>()
                       .Where(declaration => declaration.Name.ToString().Contains("AddFromAssembly"))
                       .Where(declaration => declaration.Parent is InvocationExpressionSyntax)
                       .Where(declaration => model.GetSymbolInfo(declaration.Parent).Symbol is IMethodSymbol)
                       .Select(declaration => new Container<InvocationExpressionSyntax, IMethodSymbol>((InvocationExpressionSyntax)declaration.Parent, (IMethodSymbol)model.GetSymbolInfo(declaration.Parent).Symbol));

            // Each return the container for each class
            foreach (var expression in addFromAssemblyExpressions)
            {
                // 1 Argument means the default parameter was omitted
                // We will replace it, if it's valid
                // 2 Arguments means that the default parameter was defined, and we should obey it.
                if (expression.Declaration.ArgumentList.Arguments.Count() == 1 || (expression.Declaration.ArgumentList.Arguments.Count() > 1 && expression.Declaration.ArgumentList.Arguments[1].ToString() == "true"))
                {
                    ClassDeclarationSyntax classSyntax;
                    SyntaxNode parent = expression.Declaration;
                    while (!(parent is ClassDeclarationSyntax))
                    {
                        parent = parent.Parent;
                    }
                    classSyntax = parent as ClassDeclarationSyntax;
                    Console.WriteLine(classSyntax.Identifier);
                    Console.WriteLine(expression.Declaration.ArgumentList.Arguments[0].ToString());

                    var replace = false;

                    var typeofExpression = expression.Declaration.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                    if (typeofExpression != null)
                    {
                        if (typeofExpression.Type.ToString() == classSyntax.Identifier.ToString())
                        {
                            replace = true;
                        }
                    }

                    var thisExpression = expression.Declaration.ArgumentList.Arguments[0].Expression as ThisExpressionSyntax;
                    if (thisExpression != null)
                    {
                        replace = true;
                    }

                    if (replace)
                    {
                        //Console.WriteLine(expression.Declaration.ArgumentList.Arguments[1].ToString());
                        yield return expression;
                    }
                }
            }
        }

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

        public IEnumerable<Func<string, IEnumerable<StatementSyntax>>> GetStatements(IBeforeCompileContext context, IEnumerable<Container<ClassDeclarationSyntax, INamedTypeSymbol>> containers)
        {
            foreach (var container in containers)
            {
                yield return GetStatement(context, container.Symbol, container.Declaration);
            }
        }

        public Func<string, IEnumerable<StatementSyntax>> GetStatement(IBeforeCompileContext context, INamedTypeSymbol symbol, ClassDeclarationSyntax declaration)
        {
            var attributeSymbol = symbol.GetAttributes().Single(x => x.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute"));
            var attributeDeclaration = declaration.AttributeLists
                .SelectMany(z => z.Attributes)
                .Single(z => z.Name.ToString().Contains("ImplementationOf"));

            var implementationType = symbol.ToDisplayString();
            var implementationQualifiedName = BuildQualifiedName(implementationType);

            string serviceType = null;
            IEnumerable<NameSyntax> serviceQualifiedNames = symbol.AllInterfaces
                .Select(z => BuildQualifiedName(z.ToDisplayString()));

            if (declaration.Modifiers.Any(z => z.RawKind == (int)SyntaxKind.PublicKeyword))
            {
                serviceQualifiedNames = serviceQualifiedNames.Union(new NameSyntax[] { implementationQualifiedName });
            }

            if (attributeSymbol.ConstructorArguments.Count() > 0 && attributeSymbol.ConstructorArguments[0].Value != null)
            {
                Console.WriteLine(attributeSymbol.ConstructorArguments[0].Value);
                serviceType = attributeSymbol.ConstructorArguments[0].Value.ToString();
                serviceQualifiedNames = new NameSyntax[] { BuildQualifiedName(serviceType) };
            }


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
            if (serviceType != null && !potentialBaseTypes.Any(z => serviceType.Equals(z, StringComparison.OrdinalIgnoreCase)))
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
            return GetCollectionExpressionStatement(lifecycle, serviceQualifiedNames, implementationQualifiedName);
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

        public Func<string, IEnumerable<StatementSyntax>> GetCollectionExpressionStatement(string lifecycle, IEnumerable<NameSyntax> serviceQualifiedNames, NameSyntax implementationQualifiedName)
        {
            // I hear there is a better way to do this... that will be released sometime.
            return (string identifierName) =>
            {
                return serviceQualifiedNames.Select(serviceName => SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(identifierName),
                                name: SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("Add" + lifecycle))
                            ),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(
                                    new[] {
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(serviceName)),
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(implementationQualifiedName))
                                    })
                            )
                        )
                    )
                );
            };
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
                });


            Console.WriteLine(string.Join(", ", context.CSharpCompilation.GetDiagnostics().Select(z => z.ToString())));
            var addAssemblyMethodCalls = containers
                .SelectMany(ctx => GetAddFromAssemblyMethodCall(ctx.SyntaxTree, ctx.Model));

            // Build the registration statements out of the containers.
            var nodes = GetStatements(context, containers.SelectMany(ctx => GetClassesWithImplementationAttribute(ctx.SyntaxTree, ctx.Model)))
                .ToArray();

            var newCompilation = context.CSharpCompilation;

            foreach (var method in addAssemblyMethodCalls)
            {
                var identifierName = ((MemberAccessExpressionSyntax)method.Declaration.Expression).Expression.ToString();
                Console.WriteLine(identifierName);

                var methodNodes = nodes.SelectMany(x => x(identifierName));

                var oldSyntaxRoot = method.Declaration.SyntaxTree.GetRoot();
                var newSyntaxRoot = oldSyntaxRoot
                    .ReplaceNode(method.Declaration.Parent, methodNodes)
                    .NormalizeWhitespace();

                var oldSyntaxTree = method.Declaration.SyntaxTree;
                var newSyntaxTree = oldSyntaxTree.WithRootAndOptions(newSyntaxRoot, oldSyntaxTree.Options);

                newCompilation = newCompilation.ReplaceSyntaxTree(oldSyntaxTree, newSyntaxTree);
            }



            /*// Build our new extension method.
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
            var newCompilation = context.CSharpCompilation.AddSyntaxTrees(newSyntaxTree);*/

            // Replace the compliation with our new one.
            context.CSharpCompilation = newCompilation;
        }

        public void AfterCompile(IAfterCompileContext context)
        {
            // Not Used
        }
    }
}
