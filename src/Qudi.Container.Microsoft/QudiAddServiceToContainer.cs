using System;
using Microsoft.Extensions.DependencyInjection;
using Qudi.Container.Core;
using Qudi.Core.Internal;

namespace Qudi.Container.Microsoft;

/// <summary>
/// Provides extension methods for registering services in Qudi.
/// </summary>
public static class QudiAddServiceToContainer
{
    /// <summary>
    /// Registers Qudi-collected service definitions into <paramref name="services" />.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Runtime registration configuration.</param>
    public static IServiceCollection AddQudiServices(
        IServiceCollection services,
        QudiConfiguration configuration
    )
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var registrationGraph = QudiRegistrationGraphBuilder.Build(configuration);
        var adapter = new MicrosoftContainerRegistrationAdapter(services);

        QudiContainerRegistrationEngine.RegisterBaseServices(adapter, registrationGraph.BaseRegistrations);
        QudiContainerRegistrationEngine.ApplyLayeredRegistrations(adapter, registrationGraph.LayersByService);

        return services;
    }
}
