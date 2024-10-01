using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConfigurableTaskGenerator;

[Generator]
public class ConfigurableTaskSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ConfigurableTaskSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not ConfigurableTaskSyntaxReceiver receiver)
            return;

        // Retrieve the compilation and semantic model
        Compilation compilation = context.Compilation;

        ConfigurableTaskAwaiterGenerator.GenerateAwaiterClasses(context, receiver, compilation);

        //GeneratePartialClassMethods(context, receiver, compilation);

        PartialClassGenerator.GeneratePartialClassMethods(context, receiver, compilation);
        AttributeGenerator.GenerateAttributes(context, receiver, compilation);
    }
}