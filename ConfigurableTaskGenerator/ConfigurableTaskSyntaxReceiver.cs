using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace ConfigurableTaskGenerator;

public class ConfigurableTaskSyntaxReceiver : ISyntaxReceiver
{
    public List<TypeDeclarationSyntax> ConfigurableTaskClasses { get; } = [];
    public List<TypeDeclarationSyntax> PartialClasses { get; } = [];

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Check if the syntax node is a class declaration with the specific attribute
        if (syntaxNode is TypeDeclarationSyntax classDecl)
        {
            CollectClass(classDecl);
        }

        //if(syntaxNode is RecordDeclarationSyntax recordDecl)
        //{
        //    CollectRecord(recordDecl);
        //}
    }

    private void CollectClass(TypeDeclarationSyntax classDecl)
    {
        if (IsConfigurableTaskClass(classDecl))
        {
            ConfigurableTaskClasses.Add(classDecl);
        }
        else if (IsInPartialClass(classDecl))
        {
            PartialClasses.Add(classDecl);
        }
    }

    private static bool IsInPartialClass(TypeDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }

    private static bool IsConfigurableTaskClass(TypeDeclarationSyntax classDecl)
    {
        return classDecl.AttributeLists.Any(al => al.Attributes.Any(a => IsConfigurableTaskAttrib(a)));
    }

    private static bool IsConfigurableTaskAttrib(AttributeSyntax a)
    {
        var name = a.Name.ToString();

        return name.Equals("CreateConfigurableTaskAttribute", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CreateConfigurableTask", StringComparison.OrdinalIgnoreCase);
    }
}