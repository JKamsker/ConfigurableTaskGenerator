using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace ConfigurableTaskGenerator;

public class ConfigurableTaskSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> ConfigurableTaskClasses { get; } = new List<ClassDeclarationSyntax>();
    public List<ClassDeclarationSyntax> PartialClasses { get; } = new List<ClassDeclarationSyntax>();


    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Check if the syntax node is a class declaration with the specific attribute
        if (syntaxNode is ClassDeclarationSyntax classDecl)
        {
            CollectClass(classDecl);
        }
    }

    private void CollectClass(ClassDeclarationSyntax classDecl)
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

    private static bool IsInPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }

    private static bool IsConfigurableTaskClass(ClassDeclarationSyntax classDecl)
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