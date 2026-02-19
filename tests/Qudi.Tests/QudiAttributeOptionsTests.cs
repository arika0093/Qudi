using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class QudiAttributeOptionsTests
{
    [Test]
    public void AsTypesFallback_SelfOnly_RegistersSelfOnly()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        provider.GetService<IAsTypesSelfOnlySample>().ShouldBeNull();

        var self = provider.GetRequiredService<AsTypesSelfOnlySample>();
        self.Id.ShouldBe("self-only");
    }

    [Test]
    public void AsTypesFallback_InterfacesOnly_RegistersInterfaceOnly()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        provider.GetService<AsTypesInterfacesOnlySample>().ShouldBeNull();

        var service = provider.GetRequiredService<IAsTypesInterfacesOnlySample>();
        service.Id.ShouldBe("interfaces-only");
    }

    [Test]
    public void AsTypesFallback_SelfWithInterfaces_RegistersBoth()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var self = provider.GetRequiredService<AsTypesSelfWithInterfacesSample>();
        var service = provider.GetRequiredService<IAsTypesSelfWithInterfacesSample>();

        ReferenceEquals(self, service).ShouldBeTrue();
        self.Id.ShouldBe("self-with-interface");
    }

    [Test]
    public void DuplicateHandling_Add_AllowsMultipleRegistrations()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var servicesAll = provider.GetServices<IDuplicateAddSample>().ToList();

        servicesAll.Count.ShouldBe(2);
        servicesAll.Select(s => s.Id).ShouldBe(new[] { "add-first", "add-second" });
    }

    [Test]
    public void DuplicateHandling_Skip_SkipsDuplicates()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var servicesAll = provider.GetServices<IDuplicateSkipSample>().ToList();

        servicesAll.Count.ShouldBe(1);
        servicesAll[0].Id.ShouldBe("skip-first");
    }

    [Test]
    public void DuplicateHandling_Replace_ReplacesExisting()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var servicesAll = provider.GetServices<IDuplicateReplaceSample>().ToList();

        servicesAll.Count.ShouldBe(1);
        servicesAll[0].Id.ShouldBe("replace-second");
        provider.GetRequiredService<IDuplicateReplaceSample>().Id.ShouldBe("replace-second");
    }

    [Test]
    public void DuplicateHandling_Throw_ThrowsOnDuplicate()
    {
        // NOTE: Should.Throw does not work here in AOT environment,
        // so we need to catch the exception manually.
        var throwed = false;
        var services = new ServiceCollection();
        try
        {
            services.AddQudiServices(builder => builder.SetCondition("ThrowTest"));
            var provider = services.BuildServiceProvider();
            var samples = provider
                .GetRequiredService<IEnumerable<IDuplicateThrowSample>>()
                .ToList();
            samples.Count.ShouldBe(1);
        }
        catch (InvalidOperationException)
        {
            throwed = true;
        }
        throwed.ShouldBeTrue();
    }
}
