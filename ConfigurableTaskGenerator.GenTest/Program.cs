using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using SourceGeneratorTestHelpers;

using System.Text;

namespace ConfigurableTaskGenerator.GenTest;

internal class Program
{
    static void Main(string[] args)
    {
        var result = SourceGenerator.Run<ConfigurableTaskSourceGenerator>("""
            namespace ConfigurableTaskGenerator.TestApp;
            
            public class CreateConfigurableTaskAttribute : System.Attribute
            {
                public CreateConfigurableTaskAttribute()
                {
                }
            }

            [CreateConfigurableTask]
            public class SomeArgs
            {
                public string SomeStuff { get; set; }
                public string SomeStuff1 { get; set; }

                public SomeArgs WithSomeStuff(string someStuff)
                {
                    SomeStuff = someStuff;
                    return this;
                }
            }
            """);

    }
}

internal class ConfigurableTaskSourceGenerator : ISourceGenerator
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

        foreach (var classDecl in receiver.CandidateClasses)
        {
            SemanticModel model = compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                continue;

            // Generate source code for each candidate class
            string sourceCode = GenerateSourceCode(classSymbol);
            context.AddSource($"{classSymbol.Name}_TaskGenerator.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private string GenerateSourceCode(INamedTypeSymbol classSymbol)
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
            return await _taskFactory(_args);
        }}

        public TaskAwaiter<T> GetAwaiter()
        {{
            return AwaitAsync().GetAwaiter();
        }}

");
        var signatures = new List<string>();

        // Generate Methods for each Method in SomeArgs
        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            // exclude properties
            if (member.MethodKind == MethodKind.PropertyGet || member.MethodKind == MethodKind.PropertySet)
                continue;

            // exclude constructors
            if (member.MethodKind == MethodKind.Constructor)
                continue;

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

        // Generate WithMethods for each property in SomeArgs
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
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
}

public class ConfigurableTaskSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Check if the syntax node is a class declaration with the specific attribute
        if (syntaxNode is not ClassDeclarationSyntax classDecl)
        {
            return;
        }

        if (classDecl.AttributeLists.Any(al => al.Attributes.Any(a => IsConfigurableTaskAttrib(a))))
        {
            CandidateClasses.Add(classDecl);
        }
    }

    private static bool IsConfigurableTaskAttrib(AttributeSyntax a)
    {
        var name = a.Name.ToString();

        return name.Equals("CreateConfigurableTaskAttribute", StringComparison.OrdinalIgnoreCase)
            || name.Equals("CreateConfigurableTask", StringComparison.OrdinalIgnoreCase);
    }
}