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

        GeneratePartialClassMethods(context, receiver, compilation);

        AttributeGenerator.GenerateAttributes(context, receiver, compilation);
    }

   

    private void GeneratePartialClassMethods(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        foreach (var classDecl in receiver.PartialClasses)
        {
            SemanticModel model = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                continue;

            // Generate source code for each candidate class
            string sourceCode = GeneratePartialClassMethods(classSymbol, receiver, compilation);
            context.AddSource($"{classSymbol.Name}_ConfigurableTaskWrap.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }


    /*
     Creates overloads for methods like "Task<string> DoSomething1(SomeArgs data, string additionalString)" to "SomeArgsAwaiter<string> DoSomething1Async(string additionalString)"
     */
    private string GeneratePartialClassMethods(INamedTypeSymbol classSymbol, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;

        StringBuilder sourceBuilder = new StringBuilder($@"
using System;
using System.Threading.Tasks;

namespace {namespaceName}
{{
    public partial class {className}
    {{
");

        // We need a list of all args classes that are used in the methods of the class

        var members = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary);

        var memberBuilder = new StringBuilder();
        var parameterTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // we are now in for eg: SomeService class.
        // Iterate over all the methods in the class and generate wrapper methods that overload the original method but do not contain the SomeArgs parameter (Which will be created by the generator)
        foreach (var member in members)
        {
            if (member.ReturnType is not INamedTypeSymbol returnType)
            {
                continue;
            }

            var isGenericTask = returnType.IsGenericType
                && returnType.Name == "Task";

            if (!isGenericTask)
            {
                continue;
            }

            // find param that is in the ConfigurableTaskClasses list
            if (!TryPickParam(receiver, member, out var param, out var parameters))
            {
                continue;
            }

            parameterTypes.Add(param.Type);

            // Turn "Task<string>" into "SomeArgsAwaiter<string>"
            var newReturnType = $"{param.Type.Name}Awaiter<{returnType.TypeArguments.FirstOrDefault()}>";

            var signature = $"public {newReturnType} {member.Name}({string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"))})";



            //callParameters but with args instead of the original parameter
            var callParameters = string.Join(", ", member.Parameters.Select(p => p.Equals(param, SymbolEqualityComparer.Default) ? "args" : p.Name));

            var factoryName = $"_{param.Type.Name.FirstCharToLower()}Factory";

            memberBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            return new {{newReturnType}}({{factoryName}}(this),args => {{member.Name}}({{callParameters}}));
                            
                        }
                """);

        }

        // Current servicename: SomeService
        // parameterTypes for eg: SomeArgs
        // we need: private Func<SomeService, SomeArgs> _someArgsFactory = _ => new SomeArgs();

        //var privateFields = string.Join("\n", parameterTypes.Select(p => $"private Func<{className}, {p}> _{p.FirstCharToLower()}Factory = _ => new {p}();"));
        foreach (var parameterType in parameterTypes)
        {
            var displayName = parameterType.ToDisplayString(); // SomeArgs<string>
            var typeName = parameterType.Name; // SomeArgs

            sourceBuilder.AppendLine($$"""
                        private Func<{{className}}, {{displayName}}> _{{typeName.FirstCharToLower()}}Factory = _ => new {{displayName}}();
                """);
        }

        sourceBuilder.Append(memberBuilder);

        sourceBuilder.Append("}\n}");

        var result = sourceBuilder.ToString();

        return result;
    }

    private static bool TryPickParam(ConfigurableTaskSyntaxReceiver receiver, IMethodSymbol member, out IParameterSymbol identifier, out IEnumerable<IParameterSymbol> parameters)
    {
        var param = member.Parameters.FirstOrDefault(p => receiver.ConfigurableTaskClasses.Any(c => c.Identifier.Text == p.Type.Name));
        identifier = param;
        if (param == null)
        {
            parameters = null;
            return false;
        }

        parameters = member.Parameters.Where(p => !SymbolEqualityComparer.Default.Equals(p.Type, param.Type));
        return param != null;
    }

}
