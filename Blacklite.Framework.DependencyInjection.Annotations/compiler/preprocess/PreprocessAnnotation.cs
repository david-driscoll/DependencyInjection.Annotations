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

namespace Blacklite.Framework.DependencyInjection.Annotations.Compiler
{
    public class PreprocessAnnotation : ICompileModule
    {
        public void BeforeCompile(IBeforeCompileContext context)
        {
            var statements = new List<CodeAnalysisExtensions.Container<ClassDeclarationSyntax, INamedTypeSymbol>>();
            Console.WriteLine("Assembly Name: {0}", context.CSharpCompilation.AssemblyName);

            foreach (var cxt in from tree in context.CSharpCompilation.SyntaxTrees select new { Model = context.CSharpCompilation.GetSemanticModel(tree), Tree = tree, Root = tree.GetRoot() })
            {
                Console.WriteLine("File Path: {0}", cxt.Tree.FilePath);
                var classesWithAttribute = CodeAnalysisExtensions.GetClasses(cxt.Model)
                    .Where(x => x.Symbol.GetAttributes()
                        .Any(z => z.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute")));

                foreach (var container in classesWithAttribute)
                {
                    statements.Add(container);
                }
            }

            Console.WriteLine(statements.Any());

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
                var attribute = symbol.GetAttributes().Single(x => x.AttributeClass.Name.ToString().Contains("ImplementationOfAttribute"));

                var serviceType = attribute.ConstructorArguments[0].Value.ToString();
                var serviceName = buildNameSyntax(serviceType);

                var implementationType = symbol.ToDisplayString();
                var implementationName = buildNameSyntax(implementationType);

                string lifecycle;
                switch ((int)attribute.ConstructorArguments[1].Value)
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
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(serviceName)),
                                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(implementationName))
                                    })
                                )
                            )
                    );

                nodes.Add(expression);
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
            var newSyntaxTree = SyntaxFactory.SyntaxTree(newCompilationUnit, context.CSharpCompilation.SyntaxTrees[0].Options, "", Encoding.UTF8);

            Console.WriteLine(newCompilationUnit.GetText());

            var newCompilation = context.CSharpCompilation.AddSyntaxTrees(newSyntaxTree);

            context.CSharpCompilation = newCompilation;
        }

        public void AfterCompile(IAfterCompileContext context)
        {

        }
    }
}
