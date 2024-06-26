﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using SourceGeneratorTestHelpers;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

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




    }



}
