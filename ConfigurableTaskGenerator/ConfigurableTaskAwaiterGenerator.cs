using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var skipSetterGenerationOnProperty = property.GetAttributes().Any(a => IsSetterGenerationAttribute(a));
            if (skipSetterGenerationOnProperty)
                continue;

            var hasPublicSetter = property.SetMethod != null && property.SetMethod.DeclaredAccessibility == Accessibility.Public;
            if (!hasPublicSetter)
                continue;

            var paramName = property.Name.FirstCharToLower();
            //var paramType = member.Type.Name;
            var paramType = property.Type.ToDisplayString();
            var signature = $"public {awaiterClassName}<T> With{property.Name}({paramType} {paramName})";
            if (signatures.Contains(signature))
                continue;

            // Get XML comments for the property
            var xmlComments = GetXmlComments(property);

            // Generate the setter method with XML comments
            sourceBuilder.AppendLine(xmlComments);

            sourceBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            _args.{{property.Name}} = {{paramName}};
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
        var members = classSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(x => x.DeclaredAccessibility != Accessibility.Private)
            .Where(x => x.MethodKind == MethodKind.Ordinary)
            .Where(x => IsNameLegal(x));

        // Generate Methods for each Method in SomeArgs
        foreach (var member in members)
        {
            var methodName = member.Name;

            // Handle generic methods
            var typeParameters = member.TypeParameters.Any()
                ? $"<{string.Join(", ", member.TypeParameters.Select(tp => tp.Name))}>"
                : string.Empty;

            // Collect generic constraints, if any
            var constraints = GetGenericConstraints(member);

            var parameters = string.Join(", ", member.Parameters.Select(p => $"{p.Type} {p.Name}"));
            var parameterNames = string.Join(", ", member.Parameters.Select(p => p.Name));

            // Get XML comments
            var xmlComments = GetXmlComments(member);

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

                // Build the method signature including type parameters, constraints, and XML comments
                sourceBuilder.AppendLine(xmlComments);
                sourceBuilder.AppendLine($$"""
                public {{awaiterClassName}}<T> {{methodName}}{{typeParameters}}({{parameters}})
                {{constraints}}
                {
                    Func<Task> taskFactory = async () => await _args.{{methodName}}({{parameterNames}});
                    _taskGenerators.Add(taskFactory);
                    return this;
                }
            """);
            }
            else
            {
                // Non-async methods
                var signature = $"public {awaiterClassName}<T> {member.Name}{typeParameters}({parameters}) {constraints}";

                sourceBuilder.AppendLine(xmlComments);
                sourceBuilder.AppendLine($$"""
                {{signature}}
                {
                    _args.{{methodName}}({{parameterNames}});
                    return this;
                }
            """);

                signatures.Add(signature);
            }
        }
    }

    /// <summary>
    /// Retrieves the generic constraints for a given method.
    /// </summary>
    private static string GetGenericConstraints(IMethodSymbol method)
    {
        var constraints = new List<string>();

        foreach (var typeParam in method.TypeParameters)
        {
            var paramConstraints = new List<string>();

            // Check for class/struct constraints (reference type or value type)
            if (typeParam.HasReferenceTypeConstraint)
                paramConstraints.Add("class");
            if (typeParam.HasValueTypeConstraint)
                paramConstraints.Add("struct");

            // Add constraints on specific types (e.g., TEntity : IEntity)
            paramConstraints.AddRange(typeParam.ConstraintTypes.Select(t => t.ToDisplayString()));

            // Add constructor constraint (new())
            if (typeParam.HasConstructorConstraint)
                paramConstraints.Add("new()");

            if (paramConstraints.Any())
            {
                constraints.Add($"where {typeParam.Name} : {string.Join(", ", paramConstraints)}");
            }
        }

        return constraints.Any() ? string.Join(" ", constraints) : string.Empty;
    }

    private static HashSet<string> _illegalNames = [
        "GetHashCode",
        "PrintMembers",
        "Equals",
        "<Clone>$",
        "GetAwaiter",
        "AwaitAsync",
        "ToString",
    ];

    private static bool IsNameLegal(IMethodSymbol symbol)
    {
        return !_illegalNames.Contains(symbol.Name);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage
    (
        "MicrosoftCodeAnalysisCorrectness",
        "RS1035:Do not use APIs banned for analyzers",
        Justification = "<Pending>"
    )]
    private static string GetXmlComments(ISymbol symbol)
    {
        var xmlComment = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlComment))
        {
            var xmlCommentLines = xmlComment.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(Environment.NewLine, xmlCommentLines.Select(line => $"/// {line.Trim()}"));
        }

        return string.Empty;
    }
}