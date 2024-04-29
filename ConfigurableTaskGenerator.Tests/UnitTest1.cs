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

        var sourceText = result.Results.First().GeneratedSources.First(x => x.HintName == "SomeArgs_TaskGenerator.g.cs").SourceText.ToString();
        Assert.NotNull(sourceText);
        Assert.NotEmpty(sourceText);


        // Assertations:
        // 1: only one WithXYZ method generated
        // 2: only one WithXZ method generated
        Assert.Contains("public SomeArgsAwaiter<T> WithXYZ(string xYZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        var count = CountStringOccurrences(sourceText, "public SomeArgsAwaiter<T> WithXYZ(string xYZ)");
        Assert.Equal(1, count);

        Assert.Contains("public SomeArgsAwaiter<T> WithYZ(string yZ)", sourceText, StringComparison.OrdinalIgnoreCase);
        count = CountStringOccurrences(sourceText, "public SomeArgsAwaiter<T> WithYZ(string yZ)");
        Assert.Equal(1, count);

    }

    private int CountStringOccurrences(string text, string pattern)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
        {
            i += pattern.Length;
            count++;
        }
        return count;
    }
}