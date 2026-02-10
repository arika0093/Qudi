using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class ConditionRegistrationTests
{
    private const string EnvKey = "QUDI_TEST_ENV";

    [Test]
    public void DoesNotRegisterConditionalServicesWithoutConditions()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<IConditionSample>().ToList();

        registered.ShouldBeEmpty();
    }

    [Test]
    public void RegistersConditionalServicesFromEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable(EnvKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvKey, "Testing");

            var services = new ServiceCollection();
            services.AddQudiServices(conf => conf.SetConditionFromEnvironment(EnvKey));

            var provider = services.BuildServiceProvider();
            var registered = provider.GetServices<IConditionSample>().ToList();

            registered.Count.ShouldBe(1);
            registered[0].ShouldBeOfType<ConditionSampleTesting>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvKey, original);
        }
    }
}
