using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qudi.Generator.Container;
using Qudi.Generator.Registration;
using Qudi.Generator.Utility;

namespace Qudi.Generator;

[Generator]
public sealed partial class QudiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(sp => sp.AddQudiAttributeCodes());

        var registrations = RegistrationAttrParser.QudiAttributeRegistration(context);

        var dependencies = DependsCollector.QudiProjectDependencies(context);

        var combined = registrations.Combine(dependencies);

        context.RegisterSourceOutput(
            combined,
            static (spc, source) => Execute(spc, source.Left, source.Right)
        );
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<RegistrationSpec?> registrations,
        ProjectInfo projectInfo
    )
    {
        // RegistrationInfos
        RegistrationCodeGenerator.GenerateAddQudiServicesCode(context, registrations, projectInfo);

        // AddServices
        AddServiceCodeGenerator.GenerateAddQudiServicesCode(context, projectInfo);
    }
}
