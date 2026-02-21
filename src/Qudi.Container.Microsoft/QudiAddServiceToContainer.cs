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
    /// Registers Qudi-collected service definitions into the configured service collection.
    /// </summary>
    /// <param name="configuration">Runtime registration configuration.</param>
    public static IServiceCollection AddQudiServices(QudiMicrosoftConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }
        var services = configuration.Services;
        var registrationGraph = QudiRegistrationGraphBuilder.Build(configuration);
        var adapter = new MicrosoftContainerRegistrationAdapter(services);

        QudiContainerRegistrationEngine.RegisterBaseServices(
            adapter,
            registrationGraph.BaseRegistrations
        );
        QudiContainerRegistrationEngine.ApplyLayeredRegistrations(
            adapter,
            registrationGraph.LayersByService
        );

        return services;
    }
}
