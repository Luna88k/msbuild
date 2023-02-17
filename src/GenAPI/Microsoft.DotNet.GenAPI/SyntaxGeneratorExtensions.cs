// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        /// <summary>
        /// Creates a declaration matching an existing symbol.
        ///     The reason of having this similar to `SyntaxGenerator.Declaration` extension method is that
        ///     SyntaxGenerator does not generates attributes neither for types, neither for members.
        /// </summary>
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol, ISymbolFilter symbolFilter)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                    case TypeKind.Struct:
                    case TypeKind.Interface:
                        TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return typeDeclaration
                            .WithBaseList(syntaxGenerator.GetBaseTypeList(type, symbolFilter))
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>());

                    case TypeKind.Enum:
                        EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return enumDeclaration.WithMembers(new SeparatedSyntaxList<EnumMemberDeclarationSyntax>());
                }
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol method = (IMethodSymbol)symbol;
                if (method.MethodKind == MethodKind.Constructor)
                {
                    INamedTypeSymbol? baseType = method.ContainingType.BaseType;
                    if (baseType != null)
                    {
                        IEnumerable<IMethodSymbol> baseConstructors = baseType.Constructors.Where(symbolFilter.Include);
                        // If the base type does not have default constructor.
                        if (baseConstructors.Any() && baseConstructors.All(c => !c.Parameters.IsEmpty))
                        {
                            IOrderedEnumerable<IMethodSymbol> baseTypeConstructors = baseConstructors
                                .Where(c => c.GetAttributes().All(a => !a.IsObsoleteWithUsageTreatedAsCompilationError()))
                                .OrderBy(c => c.Parameters.Length);

                            if (baseTypeConstructors.Any())
                            {
                                ConstructorDeclarationSyntax declaration = (ConstructorDeclarationSyntax)syntaxGenerator.Declaration(method);
                                return declaration.WithInitializer(GenerateBaseConstructorInitializer(baseTypeConstructors.First()));
                            }
                        }
                    }
                }
            }

            if (symbol is IEventSymbol eventSymbol)
            {
                if (eventSymbol.IsAbstract)
                {
                    // adds abstract keyword.
                    EventFieldDeclarationSyntax eventDeclaration = (EventFieldDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                    return eventDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                }
                else
                {
                    // adds generation of add & remove accessors for the non abstract events.
                    return syntaxGenerator.CustomEventDeclaration(eventSymbol.Name,
                        syntaxGenerator.TypeExpression(eventSymbol.Type),
                        eventSymbol.DeclaredAccessibility,
                        DeclarationModifiers.From(eventSymbol));
                }
            }

            try
            {
                return syntaxGenerator.Declaration(symbol);
            }
            catch (ArgumentException ex)
            {
                // re-throw the ArgumentException with the symbol that caused it.
                throw new ArgumentException(ex.Message, symbol.ToDisplayString());
            }
        }

        private static ConstructorInitializerSyntax GenerateBaseConstructorInitializer(IMethodSymbol baseTypeConstructor)
        {
            ConstructorInitializerSyntax constructorInitializer = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer);

            foreach (IParameterSymbol parameter in baseTypeConstructor.Parameters)
            {
                IdentifierNameSyntax identifier;
                // If the parameter's type is known to be a value type or has top-level nullability annotation
                if (parameter.Type.IsValueType || parameter.NullableAnnotation == NullableAnnotation.Annotated)
                    identifier = SyntaxFactory.IdentifierName("default");
                else
                    identifier = SyntaxFactory.IdentifierName("default!");

                constructorInitializer = constructorInitializer.AddArgumentListArguments(SyntaxFactory.Argument(identifier));
            }

            return constructorInitializer;
        }

        // Gets the list of base class and interfaces for a given symbol <see cref="INamedTypeSymbol"/>.
        private static BaseListSyntax? GetBaseTypeList(this SyntaxGenerator syntaxGenerator,
            INamedTypeSymbol type,
            ISymbolFilter symbolFilter)
        {
            List<BaseTypeSyntax> baseTypes = new();

            if (type.TypeKind == TypeKind.Class && type.BaseType != null && symbolFilter.Include(type.BaseType))
            {
                baseTypes.Add(SyntaxFactory.SimpleBaseType((TypeSyntax)syntaxGenerator.TypeExpression(type.BaseType)));
            }

            // includes only interfaces that were not filtered out by the given <see cref="ISymbolFilter"/>.
            baseTypes.AddRange(type.Interfaces.Where(symbolFilter.Include).Select(i => SyntaxFactory.SimpleBaseType((TypeSyntax)syntaxGenerator.TypeExpression(i))));
            return baseTypes.Count > 0 ?
                SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes)) :
                null;
        }
    }
}