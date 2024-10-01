using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace ConfigurableTaskGenerator;
internal class AttributeGenerator
{
    internal static void GenerateAttributes(GeneratorExecutionContext context, ConfigurableTaskSyntaxReceiver receiver, Compilation compilation)
    {
        var sourceBuilder = new StringBuilder();

        if (compilation.GetLanguageVersion() >= LanguageVersion.CSharp10)
        {
            sourceBuilder.AppendLine("global using ConfigurableTask;");
        }

        sourceBuilder.AppendLine($$"""
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


}
