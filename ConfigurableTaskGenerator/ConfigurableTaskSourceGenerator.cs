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
        foreach (var methodDecl in receiver.GetCandidateMethods())
        {
            SemanticModel model = compilation.GetSemanticModel(methodDecl.SyntaxTree);
            var methodSymbol = model.GetDeclaredSymbol(methodDecl) as IMethodSymbol;

            if (methodSymbol == null)
                continue;

            // Generate source code for each candidate method
            string sourceCode = GeneratePartialClassMethod(methodSymbol);
            context.AddSource($"{methodSymbol.Name}_TaskGenerator.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }


    

    private void GenerateAwaiterClasses(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        foreach (var classDecl in receiver.CandidateClasses)
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

        public async Task<T> AwaitAsync()
        {{
            foreach (var taskGenerator in _taskGenerators)
            {{
                await taskGenerator();
            }}
            return await _taskFactory(_args);
        }}

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
}

public class ConfigurableTaskSyntaxReceiver : ISyntaxReceiver
{
    private HashSet<string> _candidateClassNames = new HashSet<string>();
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

    // Methods that have a parameter of a type in the candidate classes
    public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

    public IEnumerable<MethodDeclarationSyntax> GetCandidateMethods()
    {
        foreach (var method in CandidateMethods)
        {
            var isValidMethod = method.ParameterList.Parameters.Any(p => _candidateClassNames.Contains(p.Type.ToString()));
            if (!isValidMethod)
            {
                continue;
            }

            yield return method;
        }
    }

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Check if the syntax node is a class declaration with the specific attribute
        if (syntaxNode is ClassDeclarationSyntax classDecl)
        {
            CollectClass(classDecl);
        }

        if (syntaxNode is MethodDeclarationSyntax methodDecl)
        {
            CollectMethod(methodDecl);
        }
    }

    private void CollectClass(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.AttributeLists.Any(al => al.Attributes.Any(a => IsConfigurableTaskAttrib(a))))
        {
            CandidateClasses.Add(classDecl);
            _candidateClassNames.Add(classDecl.Identifier.Text);
        }
    }

    private void CollectMethod(MethodDeclarationSyntax methodDecl)
    {
        //var isInPartialClass = methodDecl.Ancestors().OfType<ClassDeclarationSyntax>().Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
        var isInPartialClass = methodDecl.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) == true;
        if (!isInPartialClass)
        {
            return;
        }

        CandidateMethods.Add(methodDecl);

        // Unreliable since the class might not be indexed yet
        //var isValidMethod = methodDecl.ParameterList.Parameters.Any(p => _candidateClassNames.Contains(p.Type.ToString()));
        //if (isValidMethod)
        //{
        //    CandidateMethods.Add(methodDecl);
        //    return;
        //}
    }

    private static bool IsConfigurableTaskAttrib(AttributeSyntax a)
    {
        var name = a.Name.ToString();

        return name.Equals("CreateConfigurableTaskAttribute", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CreateConfigurableTask", StringComparison.OrdinalIgnoreCase);
    }
}