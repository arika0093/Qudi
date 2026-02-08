using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Qudi.Generator.Container;

internal static class AddServiceCodeGenerator
{
    public static readonly HashSet<AddServiceCore> Generators = [new AddServiceForMicrosoft()];

    public static void GenerateAddQudiServicesCode(
        SourceProductionContext context,
        ProjectInfo projectInfo
    )
    {
        var builder = new IndentedStringBuilder();
        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.CommonGeneratedHeader}}
            namespace Qudi;

            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            internal static partial class QudiAddServiceExtensions
            """
        );

        using (builder.BeginScope())
        {
            foreach (var generator in Generators)
            {
                GenerateAddQudiServicesCodeEach(builder, generator, projectInfo);
            }
        }

        var source = builder.ToString();
        context.AddSource("Qudi.AddServices.g.cs", source);
    }

    public static void GenerateAddQudiServicesCodeEach(
        IndentedStringBuilder builder,
        AddServiceCore generator,
        ProjectInfo dependencyInfo
    )
    {
        var r = generator.GenerateAddQudiServicesCode(dependencyInfo);
        if (r is not null)
        {
            builder.AppendLine(r);
        }
    }
}
