using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;

using System;
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
        GenerateCreateConfigurableTaskAttribute(context, compilation);
    }

    private void GenerateCreateConfigurableTaskAttribute(GeneratorExecutionContext context, Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("ConfigurableTask.CreateConfigurableTaskAttribute") != null)
            return;

        var sourceBuilder = new StringBuilder($@"
    using System;
    namespace ConfigurableTask
    {{
        [AttributeUsage(AttributeTargets.Class)]
        public class CreateConfigurableTaskAttribute : Attribute
        {{
        }}
    }}
");

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
            context.AddSource($"{classSymbol.Name}_TaskGenerator.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
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
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var hasPublicSetter = member.SetMethod != null && member.SetMethod.DeclaredAccessibility == Accessibility.Public;
            if (!hasPublicSetter)
                continue;

            var signature = $"public {awaiterClassName}<T> With{member.Name}(string someStuff)";
            if (signatures.Contains(signature))
                continue;

            sourceBuilder.AppendLine($$"""
                        public {{awaiterClassName}}<T> With{{member.Name}}(string someStuff)
                        {
                            _args.{{member.Name}} = someStuff;
                            return this;
                        }
                """);
            signatures.Add(signature);
        }
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

    /*
     Generates wrapper methods for each method in the class that has the attribute
     For eg:
        public partial class SomeService
        {
            // Given
            private Task<string> DoSomething(SomeArgs data)
            {
                return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}");
            }

            // Generated
            public SomeArgsAwaiter<string> DoSomethingAsync()
            {
                return new SomeArgsAwaiter<string>(DoSomething);
            }

            // Given
            private Task<string> DoSomething1(SomeArgs data, string additionalString)
            {
                return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1}: {additionalString}");
            }

            // Generated
            public SomeArgsAwaiter<string> DoSomething1Async(string additionalString)
            {
                return new SomeArgsAwaiter<string>(args => DoSomething1(args, additionalString));
            }
        }
     */
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
            context.AddSource($"{classSymbol.Name}_TaskGenerator.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
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
        // we are now in for eg: SomeService class.
        // Iterate over all the methods in the class and generate wrapper methods that overload the original method but do not contain the SomeArgs parameter (Which will be created by the generator)
        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            // just relay ordinary methods
            if (member.MethodKind != MethodKind.Ordinary)
                continue;


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


            // Turn "Task<string>" into "SomeArgsAwaiter<string>"
            var newReturnType = $"{param.Type.Name}Awaiter<{returnType.TypeArguments.FirstOrDefault()}>";

            var signature = $"public {newReturnType} {member.Name}({string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"))})";



            //callParameters but with args instead of the original parameter
            var callParameters = string.Join(", ", member.Parameters.Select(p => p.Equals(param, SymbolEqualityComparer.Default) ? "args" : p.Name));

            sourceBuilder.AppendLine($$"""
                        {{signature}}
                        {
                            return new {{newReturnType}}(args => {{member.Name}}({{callParameters}}));
                            
                        }
                """);


        }

        sourceBuilder.Append("}\n}");

        return sourceBuilder.ToString();
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