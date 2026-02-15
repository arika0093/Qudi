using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi.Tests;

public sealed class CustomizationAddServiceTests
{
    [Test]
    public void AddServiceActionCanRegisterAdditionalServices()
    {
        var services = new ServiceCollection();

        services.AddQudiServices(conf =>
        {
            conf.AddService(_ =>
            {
                // Custom processing example from README: you can use the collected information
                // and also register additional services manually.
                services.AddSingleton<CustomMarker>();
            });
        });

        var provider = services.BuildServiceProvider();

        provider.GetService<CustomMarker>().ShouldNotBeNull();
    }

    [Test]
    public void AddServiceReceivesRegistrationsAfterBuilderFilterIsApplied()
    {
        var services = new ServiceCollection();
        IReadOnlyCollection<TypeRegistrationInfo>? captured = null;

        services.AddQudiServices(conf =>
        {
            conf.AddService(config =>
                {
                    captured = config.Registrations;
                })
                .AddFilter(reg =>
                    reg.Namespace.Contains("Qudi.Tests.NotFiltered", StringComparison.Ordinal)
                );
        });

        captured.ShouldNotBeNull();
        captured!.Any(r => r.Type == typeof(NotFiltered.NotFilteredService)).ShouldBeTrue();
        captured!.Any(r => r.Type == typeof(FilteredOut.FilteredService)).ShouldBeFalse();
    }

    [Test]
    public void AddServiceRespectsOnlyWorkOnDevelopmentWhenConditionIsDifferent()
    {
        var services = new ServiceCollection();
        var executed = false;

        services.AddQudiServices(conf =>
        {
            conf.SetCondition(Condition.Production);
            conf.AddService(_ => executed = true).OnlyWorkOnDevelopment();
        });

        executed.ShouldBeFalse();
    }

    private sealed class CustomMarker;
}
