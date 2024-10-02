using Microsoft.CodeAnalysis;

using SourceGeneratorTestHelpers;

using Xunit;

namespace ConfigurableTaskGenerator.Tests;

public class Tests
{
    [Fact]
    public void Test_GenerateTaskGenerator()
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
                public string XYZ { get; set; }
                public string YZ { get; set; }
                public Dictionary<string, string> Headers { get; set; }

                public SomeArgs WithSomeStuff(string someStuff)
                {
                    SomeStuff = someStuff;
                    return this;
                }

                public async Task<SomeArgs> SomeAsyncOperation(string myParam)
                {
                    await Task.Delay(10);
                    return this;
                }

                public async Task SomeAsyncOperation1(string myParam)
                {
                    await Task.Delay(10);
                    return this;
                }

                public SomeArgs WithXYZ(string xYZ)
                {
                    XYZ = xYZ;
                    return this;
                }

                private void ThisIsNotProxied(string aa)
                {
                }
            }

            internal partial class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1} (Async executed {data.AsyncMethodExecuted})");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        Assert.NotNull(gensource);

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);

        // Assertations:
        // 1: only one WithXYZ method generated
        // 2: only one WithXZ method generated
        Assert.Contains("public SomeArgsAwaiter<T> WithXYZ(string xYZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        var count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithXYZ(string xYZ)");
        Assert.Equal(1, count);

        Assert.Contains("public SomeArgsAwaiter<T> WithYZ(string yZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithYZ(string yZ)");
        Assert.Equal(1, count);

        // has withHeaders method
        Assert.Contains("public SomeArgsAwaiter<T> WithHeaders(Dictionary<string, string> headers)", sourceText, StringComparison.OrdinalIgnoreCase);
        count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithHeaders(Dictionary<string, string> headers)");
        Assert.Equal(1, count);

        // Does not have ThisIsNotProxied method
        Assert.DoesNotContain("ThisIsNotProxied", sourceText, StringComparison.OrdinalIgnoreCase);

        var simpleGen = result.ToSimpleGen();
    }

    // Tests if nonpartial class gets a wrapper class (it should not)
    [Fact]
    public void Test_GenerateTaskGenerator_NonPartialClass()
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
            }

            public class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff}");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        Assert.Null(gensource);
    }

    // Tests if SkipSetterGenerationAttribute on the CreateConfigurableTask class will skip the setter generation (WithXXX)
    [Fact]
    public void Test_GenerateTaskGenerator_SkipSetterGeneration()
    {
        var result = SourceGenerator.Run<ConfigurableTaskSourceGenerator>("""
            namespace ConfigurableTaskGenerator.TestApp;

            public class CreateConfigurableTaskAttribute : System.Attribute
            {
                public CreateConfigurableTaskAttribute()
                {
                }
            }

            public class SkipSetterGenerationAttribute : System.Attribute
            {
                public SkipSetterGenerationAttribute()
                {
                }
            }

            [CreateConfigurableTask]
            [SkipSetterGeneration]
            public class SomeArgs
            {
                public string SomeStuff { get; set; }
            }

            public partial class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff}");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        Assert.NotNull(gensource);

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);

        // has no with method
        Assert.DoesNotContain("public SomeArgsAwaiter<T> WithSomeStuff(string someStuff)", sourceText, StringComparison.OrdinalIgnoreCase);
    }

    // Check if the SkipSetterGenerationAttribute on a property will skip the setter generation (WithXXX)
    [Fact]
    public void Test_GenerateTaskGenerator_SkipSetterGeneration_Property()
    {
        var result = SourceGenerator.Run<ConfigurableTaskSourceGenerator>("""
            namespace ConfigurableTaskGenerator.TestApp;

            public class CreateConfigurableTaskAttribute : System.Attribute
            {
                public CreateConfigurableTaskAttribute()
                {
                }
            }

            public class SkipSetterGenerationAttribute : System.Attribute
            {
                public SkipSetterGenerationAttribute()
                {
                }
            }

            [CreateConfigurableTask]
            public class SomeArgs
            {
                [SkipSetterGeneration]
                public string SomeStuff { get; set; }

                public string SomeStuff1 { get; set; }
            }

            public partial class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff}");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        Assert.NotNull(gensource);

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);

        // has no with method
        Assert.DoesNotContain("public SomeArgsAwaiter<T> WithSomeStuff(string someStuff)", sourceText, StringComparison.OrdinalIgnoreCase);

        // has with method
        Assert.Contains("public SomeArgsAwaiter<T> WithSomeStuff1(string someStuff1)", sourceText, StringComparison.OrdinalIgnoreCase);
    }

    // If class has no suitable method (either no method at all or args are not marked with CreateConfigurableTaskAttribute), no wrapper class should be generated
    [Fact]
    public void Test_GenerateTaskGenerator_NoSuitableMethod()
    {
        var result = SourceGenerator.Run<ConfigurableTaskSourceGenerator>("""
            namespace ConfigurableTaskGenerator.TestApp;

            public class CreateConfigurableTaskAttribute : System.Attribute
            {
                public CreateConfigurableTaskAttribute()
                {
                }
            }

            public class SomeArgs
            {
                public string SomeStuff { get; set; }
            }

            public partial class ArgsUser
            {
                public Task<string> DoSomething(string data)
                {
                    return Task.FromResult($"Doing something with {data}");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        //var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        //Assert.Null(gensource);

        var gensource = result.Results.First().GeneratedSources.Select(x => x.SourceText).ToList();
    }

    [Fact]
    public void Record_Args_Are_Generated_Properly()
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
            public record SomeArgs
            {
                public string SomeStuff { get; set; }
                public string SomeStuff1 { get; set; }
                public string XYZ { get; set; }
                public string YZ { get; set; }
                public Dictionary<string, string> Headers { get; set; }

                public SomeArgs WithSomeStuff(string someStuff)
                {
                    SomeStuff = someStuff;
                    return this;
                }

                public async Task<SomeArgs> SomeAsyncOperation(string myParam)
                {
                    await Task.Delay(10);
                    return this;
                }

                public async Task SomeAsyncOperation1(string myParam)
                {
                    await Task.Delay(10);
                    return this;
                }

                public SomeArgs WithXYZ(string xYZ)
                {
                    XYZ = xYZ;
                    return this;
                }
            }

            internal partial class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1} (Async executed {data.AsyncMethodExecuted})");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").SourceText;
        Assert.NotNull(gensource);

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);

        // Assertations:
        // 1: only one WithXYZ method generated
        // 2: only one WithXZ method generated
        Assert.Contains("public SomeArgsAwaiter<T> WithXYZ(string xYZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        var count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithXYZ(string xYZ)");
        Assert.Equal(1, count);

        Assert.Contains("public SomeArgsAwaiter<T> WithYZ(string yZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithYZ(string yZ)");
        Assert.Equal(1, count);

        // has withHeaders method
        Assert.Contains("public SomeArgsAwaiter<T> WithHeaders(Dictionary<string, string> headers)", sourceText, StringComparison.OrdinalIgnoreCase);
        count = sourceText.CountStringOccurrences("public SomeArgsAwaiter<T> WithHeaders(Dictionary<string, string> headers)");
        Assert.Equal(1, count);

        Assert.DoesNotContain(" <Clone>$()", sourceText, StringComparison.OrdinalIgnoreCase);

        var simpleGen = result.ToSimpleGen();
    }

    [Fact]
    public void Private_Service_Methods_Are_Proxied()
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
            public record SomeArgs
            {
                public string SomeStuff { get; set; }
            }

            internal partial class ArgsUser
            {
                private Task<string> DoSomething(SomeArgs data)
                {
                }
            }
            """);

        var simpleGen = result.ToSimpleGen();

        var sourceText = simpleGen.Where(x => x.HintName == "ArgsUser_ConfigurableTaskWrap.g.cs").Select(x => x.Text).FirstOrDefault();

        Assert.Contains("public SomeArgsAwaiter<string> DoSomething()", sourceText);
    }

    [Fact]
    public void Constraints_Are_Forwardded()
    {
        var result = SourceGenerator.Run<ConfigurableTaskSourceGenerator>("""
            namespace ConfigurableTaskGenerator.TestApp;

            public class CreateConfigurableTaskAttribute : System.Attribute
            {
                public CreateConfigurableTaskAttribute()
                {
                }
            }

            public interface IEntity
            {
            }

            [CreateConfigurableTask]
            public record SomeArgs
            {
                public string SomeStuff { get; set; }

                public void ForEntity<TEntity>(TEntity entity)
                    where TEntity : IEntity
                {
                }
            }

            internal partial class ArgsUser
            {
                private Task<string> DoSomething(SomeArgs data)
                {
                }
            }
            """);

        var simpleGen = result.ToSimpleGen();

        var sourceText = simpleGen.Where(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").Select(x => x.Text).FirstOrDefault();

        Assert.Contains("public class SomeArgsAwaiter<T>", sourceText);
        Assert.Contains("where TEntity : ConfigurableTaskGenerator.TestApp.IEntity", sourceText);
    }

    // XML Comments are copied to the generated method (Properties and methods)
    [Fact]
    public void XML_Comments_Are_Copied()
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
            public record SomeArgs
            {
                /// <summary>
                /// Some stuff
                /// </summary>
                public string SomeStuff { get; set; }

                /// <summary>
                /// Some stuff1
                /// </summary>
                public string SomeStuff1 { get; set; }

                /// <summary>
                /// Haha yolo
                /// </summary>
                public string Haha(string a)
                {
                    return a;
                }
            }

            internal partial class ArgsUser
            {
                /// <summary>
                /// This does something
                /// </summary>
                private Task<string> DoSomething(SomeArgs data)
                {
                }
            }
            """);

        var simpleGen = result.ToSimpleGen();
        var sourceText = simpleGen.Where(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").Select(x => x.Text).FirstOrDefault();

        Assert.Contains("/// <summary>", sourceText);
    }
}