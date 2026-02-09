using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Container;
using Qudi.Generator.Helper;
using Qudi.Generator.Registration;

namespace Qudi.Generator;

[Generator]
public sealed partial class QudiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(sp => sp.AddQudiAttributeCodes());

        var registrations = RegistrationAttrParser.QudiAttributeRegistration(context);

        var helperTargets = HelperTargetCollector.CollectTargets(context);

        var dependencies = DependsCollector.QudiProjectDependencies(context);

        var combined = registrations.Combine(dependencies);

        context.RegisterSourceOutput(
            combined,
            static (spc, source) => Execute(spc, source.Left, source.Right)
        );

        context.RegisterSourceOutput(
            helperTargets,
            static (spc, targets) => HelperCodeGenerator.GenerateHelpers(spc, targets)
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
