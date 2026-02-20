using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class ConditionRegistrationTests
{
    private const string EnvKey = "QUDI_TEST_ENV";
    private const string TestingCondition = "Testing";
    private const string ProductionCondition = "Production";

    [Test]
    public void ConditionalServicesWithoutConditions()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<IConditionSample>().ToList();

        // When no conditions are specified, conditional services should not be registered.
        registered.ShouldBeEmpty();
    }

    [Test]
    public void RegisterCConfigurationConstantKey()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.SetCondition(Condition.Production);
        });

        var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<IConditionSample>().ToList();

        registered.Count.ShouldBe(1);
        registered[0].ShouldBeOfType<ConditionSampleProduction>();
    }

    [Test]
    public void RegisterCConfigurationDirect()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.SetCondition(TestingCondition);
        });

        var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<IConditionSample>().ToList();

        registered.Count.ShouldBe(1);
        registered[0].ShouldBeOfType<ConditionSampleTesting>();
    }

    [Test]
    public void RegistersConditionalServicesFromEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable(EnvKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvKey, TestingCondition);

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

    internal interface IConditionSample
    {
        string Marker { get; }
    }

    [DITransient(When = [TestingCondition])]
    internal sealed class ConditionSampleTesting : IConditionSample
    {
        public string Marker => "testing";
    }

    [DITransient(When = [ProductionCondition])]
    internal sealed class ConditionSampleProduction : IConditionSample
    {
        public string Marker => "production";
    }
}
