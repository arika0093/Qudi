using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Container;
using Qudi.Generator.Dependency;
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

        var projectBasicInfo = DependsCollector.QudiProjectBasicInfo(context);
        var projectInfo = DependsCollector.QudiProjectInfo(context);

        // Internal registrations - only depends on basic project info
        context.RegisterSourceOutput(
            projectBasicInfo,
            static (spc, basicInfo) =>
                RegistrationCodeGenerator.GenerateInternalRegistrationsFile(spc, basicInfo)
        );

        // Project registrations - depends on registrations and full project info
        var combinedForProjectRegistrations = registrations.Combine(projectInfo);

        context.RegisterSourceOutput(
            combinedForProjectRegistrations,
            static (spc, source) =>
            {
                var (regs, projInfo) = source;
                RegistrationCodeGenerator.GenerateProjectRegistrationsFile(
                    spc,
                    regs,
                    projInfo.Basic,
                    projInfo.Dependencies
                );
            }
        );

        // add services - only depends on basic project info
        context.RegisterSourceOutput(
            projectBasicInfo,
            static (spc, basicInfo) =>
                AddServiceCodeGenerator.GenerateAddQudiServicesCode(spc, basicInfo)
        );

        // helper (Decorator/Composite)
        context.RegisterSourceOutput(
            helperTargets,
            static (spc, targets) => HelperCodeGenerator.GenerateHelpers(spc, targets)
        );
    }
}
