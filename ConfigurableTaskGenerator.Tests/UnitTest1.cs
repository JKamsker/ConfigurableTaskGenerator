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

            }

            public partial class ArgsUser
            {
                public Task<string> DoSomething(SomeArgs data)
                {
                    return Task.FromResult($"Doing something with {data.SomeStuff} and {data.SomeStuff1} (Async executed {data.AsyncMethodExecuted})");
                }
            }
            """);

        // HardCode:		result.Results.First().GeneratedSources[0].SourceText.ToString()
        // result.Results.First().GeneratedSources where HintName == "SomeArgs_TaskGenerator.g.cs" .SourceText.ToString()

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_TaskAwaiterWrapperGenerator.g.cs").SourceText;
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
        count = sourceText.CountStringOccurrences( "public SomeArgsAwaiter<T> WithHeaders(Dictionary<string, string> headers)");
        Assert.Equal(1, count);
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

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_TaskAwaiterWrapperGenerator.g.cs").SourceText;
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

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_TaskAwaiterWrapperGenerator.g.cs").SourceText;
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

        var gensource = result.Results.First().GeneratedSources.FirstOrDefault(x => x.HintName == "ArgsUser_TaskAwaiterWrapperGenerator.g.cs").SourceText;
        Assert.NotNull(gensource);

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_AwaitableTask.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);

        // has no with method
        Assert.DoesNotContain("public SomeArgsAwaiter<T> WithSomeStuff(string someStuff)", sourceText, StringComparison.OrdinalIgnoreCase);

        // has with method
        Assert.Contains("public SomeArgsAwaiter<T> WithSomeStuff1(string someStuff1)", sourceText, StringComparison.OrdinalIgnoreCase);
    }



}