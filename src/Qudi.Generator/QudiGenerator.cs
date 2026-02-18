using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Container;
using Qudi.Generator.Dependency;
using Qudi.Generator.Helper;
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

        var helperTargets = HelperTargetCollector.CollectTargets(context);
        var projectBasicInfo = DependsCollector.QudiProjectBasicInfo(context);
        var projectInfo = DependsCollector.QudiProjectInfo(context);

        // Internal and Self registrations - depends on registrations and basic project info
        var combinedForSelfRegistrations = registrations.Combine(projectBasicInfo);

        context.RegisterSourceOutput(
            combinedForSelfRegistrations,
            static (spc, source) =>
            {
                var (regs, basicInfo) = source;
                RegistrationCodeGenerator.GenerateInternalAndSelfRegistrationsFile(
                    spc,
                    regs,
                    basicInfo
                );
            }
        );

        // WithDependencies implementation - depends on full project info
        context.RegisterImplementationSourceOutput(
            projectInfo,
            static (spc, projInfo) =>
                RegistrationCodeGenerator.GenerateWithDependenciesImplementationFile(spc, projInfo)
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
