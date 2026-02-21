using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi;

/// <summary>
/// Configuration for Microsoft.Extensions.DependencyInjection registration.
/// </summary>
public sealed record QudiMicrosoftConfiguration : QudiConfiguration
{
    /// <summary>
    /// Target service collection.
    /// </summary>
    public required IServiceCollection Services { get; init; }
}

/// <summary>
/// Builder for Microsoft.Extensions.DependencyInjection registration.
/// </summary>
public sealed class QudiMicrosoftConfigurationBuilder
    : QudiConfigurationBuilder<QudiMicrosoftConfiguration>
{
    private readonly IServiceCollection _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="QudiMicrosoftConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    public QudiMicrosoftConfigurationBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    protected override QudiMicrosoftConfiguration CreateTypedConfiguration(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    ) =>
        new()
        {
            Registrations = registrations,
            Conditions = conditions,
            Services = _services,
        };

    /// <inheritdoc />
    protected override void ExecuteTyped(QudiMicrosoftConfiguration configuration)
    {
        Qudi.Container.Microsoft.QudiAddServiceToContainer.AddQudiServices(configuration);
    }
}
