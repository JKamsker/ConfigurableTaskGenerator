using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

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

        GenerateAwaiterClasses(context, receiver, compilation);

        GeneratePartialClassMethods(context, receiver, compilation);

        // Generate CreateConfigurableTaskAttribute if it doesn't exist in the current compilation
        GenerateAttributes(context, receiver, compilation);
    }

    private void GenerateAttributes(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        //if (compilation.GetTypeByMetadataName("ConfigurableTask.CreateConfigurableTaskAttribute") != null)
        //    return;

        //var sourceBuilder = new StringBuilder($$"""

        //        using System;
        //        namespace ConfigurableTask
        //        {
        //            [AttributeUsage(AttributeTargets.Class)]
        //            public class CreateConfigurableTaskAttribute : Attribute
        //            {
        //            }
        //        }

        //    """);

        var sourceBuilder = new StringBuilder($$"""

                using System;
                namespace ConfigurableTask
                {
            """);

        if (compilation.GetTypeByMetadataName("ConfigurableTask.CreateConfigurableTaskAttribute") == null)
        {
            sourceBuilder.AppendLine($$"""
                    [AttributeUsage(AttributeTargets.Class)]
                    public class CreateConfigurableTaskAttribute : Attribute
                    {
                    }
                """);
        }

        // Attribute to skip generation of property setter methods (Works on the whole class or on a single property)
        if (compilation.GetTypeByMetadataName("ConfigurableTask.SkipSetterGenerationAttribute") == null)
        {
            sourceBuilder.AppendLine($$"""
                    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
                    public class SkipSetterGenerationAttribute : Attribute
                    {
                    }
                """);
        }


        sourceBuilder.AppendLine("}");



        context.AddSource("CreateConfigurableTaskAttribute.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private void GenerateAwaiterClasses(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
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

    private string GenerateAwaiterClass(INamedTypeSymbol classSymbol)
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
            context.AddSource($"{classSymbol.Name}_TaskAwaiterWrapperGenerator.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
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
        var parameterTypes = new HashSet<string>();

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

            parameterTypes.Add(param.Type.Name);

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
        foreach (var p in parameterTypes)
        {
            sourceBuilder.AppendLine($$"""
                        private Func<{{className}}, {{p}}> _{{p.FirstCharToLower()}}Factory = _ => new {{p}}();
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
