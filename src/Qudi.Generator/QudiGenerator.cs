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

        var dependencies = DependsCollector.QudiProjectDependencies(context);

        var combined = registrations.Combine(dependencies);

        // registrations
        context.RegisterSourceOutput(
            combined,
            static (spc, source) =>
            {
                var (regs, deps) = source;
                RegistrationCodeGenerator.GenerateRegistrationsCode(spc, regs, deps);
            }
        );

        // add services
        context.RegisterSourceOutput(
            dependencies,
            static (spc, deps) => AddServiceCodeGenerator.GenerateAddQudiServicesCode(spc, deps)
        );

        // helper (Decorator/Composite)
        context.RegisterSourceOutput(
            helperTargets,
            static (spc, targets) => HelperCodeGenerator.GenerateHelpers(spc, targets)
        );
    }
}
