using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConfigurableTaskGenerator;

internal static class PartialClassGenerator
{
    public static void GeneratePartialClassMethods(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        foreach (var classDecl in receiver.PartialClasses)
        {
            SemanticModel model = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                continue;

            // Extract method details
            var methodsToGenerate = ExtractMethodsToGenerate(classSymbol, receiver);
            if (methodsToGenerate?.Any() != true)
            {
                continue;
            }

            // Generate source code only if there are methods to generate
            string sourceCode = GeneratePartialClassMethods(classSymbol, methodsToGenerate);
            if (string.IsNullOrEmpty(sourceCode))
            {
                continue;
            }

            context.AddSource($"{classSymbol.Name}_ConfigurableTaskWrap.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static string GeneratePartialClassMethods(INamedTypeSymbol classSymbol, List<MethodGenerationInfo> methodsToGenerate)
    {
        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;

        var accessModifier = classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => null
        };

        if (accessModifier == null)
        {
            return string.Empty;
        }

        StringBuilder sourceBuilder = new StringBuilder($@"
using System;
using System.Threading.Tasks;

namespace {namespaceName}
{{
    {accessModifier} partial class {className}
    {{
");

        var memberBuilder = new StringBuilder();
        var parameterTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var methodInfo in methodsToGenerate)
        {
            var member = methodInfo.OriginalMethod;
            var param = methodInfo.Param;
            var parameters = methodInfo.Parameters;

            parameterTypes.Add(param.Type);

            var signature = $"public {methodInfo.ReturnType} {member.Name}({string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"))})";

            var callParameters = string.Join(", ", member.Parameters.Select(p => p.Equals(param, SymbolEqualityComparer.Default) ? "args" : p.Name));

            var factoryName = $"_{param.Type.Name.FirstCharToLower()}Factory";

            memberBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            return new {{methodInfo.ReturnType}}({{factoryName}}(this), args => {{member.Name}}({{callParameters}}));
                        }
                """);
        }

        foreach (var parameterType in parameterTypes)
        {
            var displayName = parameterType.ToDisplayString();
            var typeName = parameterType.Name;

            sourceBuilder.AppendLine($$"""
                        private Func<{{className}}, {{displayName}}> _{{typeName.FirstCharToLower()}}Factory = _ => new {{displayName}}();
                """);
        }

        sourceBuilder.Append(memberBuilder);
        sourceBuilder.Append("}\n}");

        return sourceBuilder.ToString();
    }

    private static List<MethodGenerationInfo>? ExtractMethodsToGenerate(INamedTypeSymbol classSymbol, ConfigurableTaskSyntaxReceiver receiver)
    {
        List<MethodGenerationInfo>? methodsToGenerate = null;

        var members = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary);

        foreach (var member in members)
        {
            //if (member.DeclaredAccessibility == Accessibility.Private)
            //{
            //    continue;
            //}

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

            if (!TryPickParam(receiver, member, out var param, out var parameters))
            {
                continue;
            }

            methodsToGenerate ??= [];
            // Add method information for generation
            methodsToGenerate.Add(new MethodGenerationInfo
            {
                OriginalMethod = member,
                Param = param,
                Parameters = parameters,
                ReturnType = $"{param.Type.Name}Awaiter<{returnType.TypeArguments.FirstOrDefault()}>"
            });
        }

        return methodsToGenerate;
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
        return true;
    }

    private class MethodGenerationInfo
    {
        public IMethodSymbol OriginalMethod { get; set; }
        public IParameterSymbol Param { get; set; }
        public IEnumerable<IParameterSymbol> Parameters { get; set; }
        public string ReturnType { get; set; }
    }
}