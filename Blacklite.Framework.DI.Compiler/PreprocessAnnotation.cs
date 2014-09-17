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
    public class PreprocessAnnotation : ICompileModule
    {
        public void BeforeCompile(IBeforeCompileContext context)
        {
            var statements = new List<CodeAnalysisExtensions.Container<ClassDeclarationSyntax, INamedTypeSymbol>>();
            //Console.WriteLine("Assembly Name: {0}", context.CSharpCompilation.AssemblyName);

            foreach (var cxt in from tree in context.CSharpCompilation.SyntaxTrees select new { Model = context.CSharpCompilation.GetSemanticModel(tree), Tree = tree, Root = tree.GetRoot() })
            {
                //Console.WriteLine("File Path: {0}", cxt.Tree.FilePath);
                //Console.WriteLine(cxt.Tree.GetText().ToString());
                var classesWithAttribute = CodeAnalysisExtensions.GetClasses(cxt.Model)
                    .Where(x => x.Symbol.GetAttributes()
                        .Any(z => z.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute")));

                foreach (var container in classesWithAttribute)
                {
                    statements.Add(container);
                }
            }

            //Console.WriteLine(statements.Any());

            var variableName = "collection";
            var identifier = SyntaxFactory.IdentifierName(variableName);

            var nodes = new List<StatementSyntax>();

            Func<string, NameSyntax> buildNameSyntax = type =>
            {
                var parts = type.Split('.');
                NameSyntax nameSyntax;

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
            };

            //var body = new StringBuilder();

            foreach (var container in statements)
            {
                var symbol = container.Symbol;
                var attributeSymbol = symbol.GetAttributes().Single(x => x.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute"));
                if (attributeSymbol.ConstructorArguments.Length == 0)
                {
                    //Console.WriteLine("ATtribute symbol is strange... {0} on {1}", attributeSymbol.ToString(), symbol.ToString());
                }
                else
                {
                    var attributeDeclaration = container.Declaration.AttributeLists
                        .SelectMany(z => z.Attributes)
                        .Single(z => z.Name.ToString().Contains("ImplementationOf"));

                    //Console.WriteLine(attributeSymbol.ToString());
                    //Console.WriteLine(string.Join(", ", attributeSymbol.ConstructorArguments.Select(z => z.ToString())));

                    var serviceType = attributeSymbol.ConstructorArguments[0].Value.ToString();
                    var serviceName = serviceType.Split('.').Last();
                    var serviceQualifiedName = buildNameSyntax(serviceType);

                    var implementationType = symbol.ToDisplayString();
                    var implementationName = implementationType.Split('.').Last();
                    var implementationQualifiedName = buildNameSyntax(implementationType);

                    var baseTypes = new List<string>();
                    var impType = symbol;
                    while (impType != null)
                    {
                        baseTypes.Add(impType.ToDisplayString());
                        impType = impType.BaseType;
                    }

                    //Console.WriteLine("Base Types: {0}", string.Join(", ", baseTypes));
                    //Console.WriteLine("Interfaces: {0}", string.Join(", ", symbol.AllInterfaces.Select(z => z.ToDisplayString())));

                    // TODO: Enforce implementation is assignable to service
                    // Diagnostic error?
                    var potentialBaseTypes = baseTypes.Concat(symbol.AllInterfaces.Select(z => z.ToDisplayString()));
                    if (!potentialBaseTypes.Any(z => serviceType.Equals(z, StringComparison.OrdinalIgnoreCase)))
                    {
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

                    string lifecycle;
                    switch ((int)attributeSymbol.ConstructorArguments[1].Value)
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


                    var expression = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                identifier, name: SyntaxFactory.IdentifierName(
                                    SyntaxFactory.Identifier("Add" + lifecycle)
                                    )
                                ),

                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SeparatedList(
                                        new[]
                                    {
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(serviceQualifiedName)),
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(implementationQualifiedName))
                                    })
                                    )
                                )
                        );

                    nodes.Add(expression);
                }
            }

            var @method = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName("IServiceCollection"), "AddImplementations")
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        SyntaxFactory.Parameter(
                            default(SyntaxList<AttributeListSyntax>),
                            default(SyntaxTokenList),
                            SyntaxFactory.IdentifierName("IServiceCollection"),
                            SyntaxFactory.Identifier("collection"),
                            null
                        )
                    )
                    .AddBodyStatements(nodes.ToArray())
                    .AddBodyStatements(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("collection")));

            var @class = SyntaxFactory.ClassDeclaration("ServicesContainerExtensions")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(@method);

            var @namespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("__generated"))
                .AddMembers(@class)
                .NormalizeWhitespace();

            var newCompilationUnit = SyntaxFactory.CompilationUnit()
                //.AddUsings(usings.Values.ToArray())
                .AddUsings(SyntaxFactory.UsingDirective(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.QualifiedName(
                            SyntaxFactory.IdentifierName("Microsoft"),
                            SyntaxFactory.IdentifierName("Framework")
                        ),
                        SyntaxFactory.IdentifierName("DependencyInjection")
                    )
                ))
                .AddMembers(@namespace)
                .NormalizeWhitespace();

            //var newSyntaxTree = SyntaxFactory.ParseSyntaxTree(newCompilationUnit.GetText().ToString(), context.CSharpCompilation.SyntaxTrees[0].Options, "", Encoding.UTF8);
            var newSyntaxTree = SyntaxFactory.SyntaxTree(newCompilationUnit, context.CSharpCompilation.SyntaxTrees[0].Options);

            //Console.WriteLine(newCompilationUnit.GetText());

            var newCompilation = context.CSharpCompilation.AddSyntaxTrees(newSyntaxTree);

            context.CSharpCompilation = newCompilation;
        }

        public void AfterCompile(IAfterCompileContext context)
        {
            // Not Used
        }
    }
}
