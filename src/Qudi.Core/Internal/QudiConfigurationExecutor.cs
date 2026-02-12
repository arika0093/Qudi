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
        QudiConfigurationMultiBuilder mb,
        Func<bool, IEnumerable<TypeRegistrationInfo>> fetchTypeFunc
    )
    {
        var _sharedBuilder = mb._sharedBuilder;
        foreach (var builder in mb._builders)
        {
            // combine each builder with shared settings
            var selfOnly = builder.UseSelfImplementsOnlyEnabled
                ?? _sharedBuilder.UseSelfImplementsOnlyEnabled
                ?? false;
            var filters = builder.Filters.Union(_sharedBuilder.Filters).ToList();
            var conditions = builder.Conditions.Union(_sharedBuilder.Conditions).ToList();
            // apply filters
            var types = fetchTypeFunc(selfOnly);
            foreach (var filter in filters)
            {
                types = types.Where(filter);
            }
            var configuration = new QudiConfiguration
            {
                Registrations = [.. types],
                Conditions = conditions,
            };
            // execute configuration action
            builder.Execute(configuration);
        }
    }
}
