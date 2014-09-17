using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Blacklite.Framework.DependencyInjection.Annotations.Compiler
{
    public static class CodeAnalysisExtensions
    {
        public class Container<TDeclaration, TSymbol>(TDeclaration declaration, TSymbol symbol)
        {
            public TDeclaration Declaration { get; } = declaration;
            public TSymbol Symbol { get; } = symbol;
        }

        public static IEnumerable<Container<MethodDeclarationSyntax, IMethodSymbol>> GetMethods(SemanticModel model)
        {
            return model.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(declaration => new Container<MethodDeclarationSyntax, IMethodSymbol>(declaration, model.GetDeclaredSymbol(declaration)));
        }

        public static Container<MethodDeclarationSyntax, IMethodSymbol> GetMethod(SemanticModel model, string name)
        {
            return GetMethods(model)
                .Where(container => container.Declaration.Identifier.Text.Equals(name))
                .Single();
        }

        public static IEnumerable<Container<ClassDeclarationSyntax, INamedTypeSymbol>> GetClasses(SemanticModel model)
        {
            return model.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(declaration => new Container<ClassDeclarationSyntax, INamedTypeSymbol>(declaration, model.GetDeclaredSymbol(declaration)));
        }

        public static Container<ClassDeclarationSyntax, INamedTypeSymbol> GetClass(SemanticModel model, string name)
        {
            return GetClasses(model)
                .Where(symbol => symbol.Declaration.Identifier.Text.Equals(name))
                .Single();
        }
    }
}