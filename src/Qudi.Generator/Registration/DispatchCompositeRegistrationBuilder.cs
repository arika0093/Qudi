using System.Collections.Generic;
using System.Collections.Immutable;
using Qudi.Generator.Helper;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Registration;

internal static class DispatchCompositeRegistrationBuilder
{
    public static ImmutableArray<RegistrationSpec?> Build(
        EquatableArray<DispatchCompositeTarget> targets
    )
    {
        if (targets.Count == 0)
        {
            return ImmutableArray<RegistrationSpec?>.Empty;
        }

        // Build registrations for closed dispatcher types generated from composite constraints
        // (e.g., IComponentValidator<IComponent> -> ComponentValidatorDispatcher__Dispatch_IComponent).
        var specs = new List<RegistrationSpec?>();
        foreach (var target in targets)
        {
            foreach (var constraint in target.ConstraintTypes)
            {
                var className = $"{target.ImplementingTypeName}__Dispatch_{constraint.Suffix}";
                var fullTypeName = string.IsNullOrEmpty(target.ImplementingTypeNamespace)
                    ? $"global::{className}"
                    : $"global::{target.ImplementingTypeNamespace}.{className}";

                var asTypes = new EquatableArray<string>([
                    $"typeof({constraint.ConstructedInterfaceTypeName})",
                ]);

                // Dispatcher constructor depends on IEnumerable<IComponentValidator<Concrete>> for each concrete type.
                var requiredTypes = new List<string>();
                foreach (var concrete in target.ConcreteTypes)
                {
                    requiredTypes.Add(
                        $"typeof(global::System.Collections.Generic.IEnumerable<{concrete.ConstructedInterfaceTypeName}>)"
                    );
                }

                specs.Add(
                    new RegistrationSpec
                    {
                        TypeName = fullTypeName,
                        Namespace = target.ImplementingTypeNamespace,
                        Lifetime = "Transient",
                        When = new EquatableArray<string>([]),
                        AsTypes = asTypes,
                        RequiredTypes = new EquatableArray<string>(requiredTypes.ToArray()),
                        UsePublic = false,
                        KeyLiteral = null,
                        Order = 0,
                        MarkAsDecorator = false,
                        MarkAsComposite = false,
                        // Mark dispatcher so container avoids layered composite handling.
                        MarkAsCompositeDispatcher = true,
                        Export = false,
                    }
                );
            }
        }

        return specs.ToImmutableArray();
    }
}
