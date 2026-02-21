using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Internal;

/// <summary>
/// Executor for Qudi configuration builders. This method will be called by Qudi internally.
/// </summary>
public static class QudiConfigurationExecutor
{
    /// <summary>
    /// Build and execute the configurations.
    /// </summary>
    public static void ExecuteAll(
        QudiConfigurationRootBuilder mb,
        Func<bool, IEnumerable<TypeRegistrationInfo>> fetchTypeFunc
    )
    {
        var _sharedBuilder = mb._sharedBuilder;
        var conditions = mb._conditions;
        foreach (var builder in mb._builders)
        {
            if (
                builder.OnlyWorkedConditions.Count > 0
                && conditions.Any(c => !builder.OnlyWorkedConditions.Contains(c))
            )
            {
                // skip this builder since its conditions do not match
                continue;
            }
            // combine each builder with shared settings
            var selfOnly =
                builder.UseSelfImplementsOnlyEnabled
                ?? _sharedBuilder.UseSelfImplementsOnlyEnabled
                ?? false;
            var filters = builder.Filters.Union(_sharedBuilder.Filters).ToList();
            // apply filters
            var types = fetchTypeFunc(selfOnly);
            foreach (var filter in filters)
            {
                types = types.Where(filter);
            }
            var registrations = (IReadOnlyCollection<TypeRegistrationInfo>)[.. types];
            // execute configuration action
            builder.ExecuteInternal(registrations, conditions);
        }
    }
}
