using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ConfigurableTaskGenerator;
internal static class ConfigurableTaskAwaiterGenerator
{

    internal static void GenerateAwaiterClasses(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        foreach (var classDecl in receiver.ConfigurableTaskClasses)
        {
            SemanticModel model = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                continue;

            // Generate source code for each candidate class
            string sourceCode = GenerateAwaiterClass(classSymbol);
            context.AddSource($"{classSymbol.Name}_AwaitableTask.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static string GenerateAwaiterClass(INamedTypeSymbol classSymbol)
    {
        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;
        var awaiterClassName = $"{className}Awaiter";

        StringBuilder sourceBuilder = new StringBuilder($@"
using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace {namespaceName}
{{
    public class {awaiterClassName}<T>
    {{
        private readonly {className} _args;
        private readonly Func<{className}, Task<T>> _taskFactory;
        private readonly List<Func<Task>> _taskGenerators = new List<Func<Task>>();

        public {awaiterClassName}(Func<{className}, Task<T>> taskFactory)
            : this(new {className}(), taskFactory)
        {{
        }}

        public {awaiterClassName}({className} args, Func<{className}, Task<T>> taskFactory)
        {{
            _args = args;
            _taskFactory = taskFactory;
        }}

        private async Task<T> AwaitAsync()
        {{
            foreach (var taskGenerator in _taskGenerators)
            {{
                await taskGenerator();
            }}
            return await _taskFactory(_args);
        }}
        
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TaskAwaiter<T> GetAwaiter()
        {{
            return AwaitAsync().GetAwaiter();
        }}

");
        var signatures = new List<string>();

        GenerateProxyMethods(classSymbol, awaiterClassName, sourceBuilder, signatures);

        // Generate WithMethods for each property in SomeArgs
        GenerateSetterMethodsForProperties(classSymbol, awaiterClassName, sourceBuilder, signatures);

        // Add implicit operator
        sourceBuilder.Append($$"""
                    public static implicit operator {{awaiterClassName}}<T>(Task<T> task)
                    {
                        return new {{awaiterClassName}}<T>(new {{className}}(), _ => task);
                    }
                }
            }
            """);

        return sourceBuilder.ToString();
    }

    private static void GenerateSetterMethodsForProperties(INamedTypeSymbol classSymbol, string awaiterClassName, StringBuilder sourceBuilder, List<string> signatures)
    {
        // check if the ``SkipSetterGenerationAttribute`` is present on the class
        var skipSetterGeneration = classSymbol.GetAttributes().Any(a => IsSetterGenerationAttribute(a));
        if (skipSetterGeneration)
            return;

        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var skipSetterGenerationOnProperty = member.GetAttributes().Any(a => IsSetterGenerationAttribute(a));
            if (skipSetterGenerationOnProperty)
                continue;

            var hasPublicSetter = member.SetMethod != null && member.SetMethod.DeclaredAccessibility == Accessibility.Public;
            if (!hasPublicSetter)
                continue;

            var paramName = member.Name.FirstCharToLower();
            //var paramType = member.Type.Name;
            var paramType = member.Type.ToDisplayString();
            var signature = $"public {awaiterClassName}<T> With{member.Name}({paramType} {paramName})";
            if (signatures.Contains(signature))
                continue;

            sourceBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            _args.{{member.Name}} = {{paramName}};
                            return this;
                        }
                """);
            signatures.Add(signature);
        }
    }

    private static bool IsSetterGenerationAttribute(AttributeData a)
    {
        //return a.AttributeClass.Name == "SkipSetterGenerationAttribute";
        return string.Equals(a.AttributeClass.Name, "SkipSetterGeneration", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.AttributeClass.Name, "SkipSetterGenerationAttribute", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void GenerateProxyMethods(INamedTypeSymbol classSymbol, string awaiterClassName, StringBuilder sourceBuilder, List<string> signatures)
    {
        // Generate Methods for each Method in SomeArgs
        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            // just relay ordinary methods
            if (member.MethodKind != MethodKind.Ordinary)
                continue;

            if (member.IsAsync)
            {
                if (!(member.ReturnsVoid == false && member.ReturnType is INamedTypeSymbol returnType))
                    continue;

                // is Task or Task<classSymbol>
                var isValidGenericTask = returnType.IsGenericType
                    && returnType.TypeArguments.FirstOrDefault()?.Equals(classSymbol, SymbolEqualityComparer.Default) == true;

                var isValidNonGenericTask = !returnType.IsGenericType
                    && returnType.Name == "Task";

                if (!(isValidGenericTask || isValidNonGenericTask))
                    continue;


                var methodName = member.Name;
                var parameters = string.Join(", ", member.Parameters.Select(p => $"{p.Type} {p.Name}"));
                sourceBuilder.AppendLine($$"""
                            public {{awaiterClassName}}<T> {{methodName}}({{parameters}})
                            {
                                Func<Task> taskFactory = async () => await _args.{{methodName}}({{string.Join(", ", member.Parameters.Select(p => p.Name))}});
                                _taskGenerators.Add(taskFactory);
                                return this;
                            }
                    """);
            }
            else
            {
                var signature = $"public {awaiterClassName}<T> {member.Name}({string.Join(", ", member.Parameters.Select(p => $"{p.Type} {p.Name}"))})";

                sourceBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            _args.{{member.Name}}({{string.Join(", ", member.Parameters.Select(p => p.Name))}});
                            return this;
                        }
                """);
                signatures.Add(signature);
            }
        }
    }

}
