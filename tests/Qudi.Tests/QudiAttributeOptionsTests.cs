using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class QudiAttributeOptionsTests
{
    private const string TestCondition = nameof(QudiAttributeOptionsTests);

    [Test]
    public void AsTypesFallback_SelfOnly_RegistersSelfOnly()
    {
        using var provider = BuildProvider();
        // Self-only should avoid interface registration.
        provider.GetService<IAsTypesSelfOnlySample>().ShouldBeNull();

        var self = provider.GetRequiredService<AsTypesSelfOnlySample>();
        self.Id.ShouldBe("self-only");
    }

    [Test]
    public void AsTypesFallback_InterfacesOnly_RegistersInterfaceOnly()
    {
        using var provider = BuildProvider();
        // Interface-only should avoid self registration.
        provider.GetService<AsTypesInterfacesOnlySample>().ShouldBeNull();

        var service = provider.GetRequiredService<IAsTypesInterfacesOnlySample>();
        service.Id.ShouldBe("interfaces-only");
    }

    [Test]
    public void AsTypesFallback_SelfWithInterfaces_RegistersBoth()
    {
        using var provider = BuildProvider();
        var self = provider.GetRequiredService<AsTypesSelfWithInterfacesSample>();
        var service = provider.GetRequiredService<IAsTypesSelfWithInterfacesSample>();

        ReferenceEquals(self, service).ShouldBeTrue();
        self.Id.ShouldBe("self-with-interface");
    }

    [Test]
    public void AsTypesFallback_SelfOrInterfaces_PrefersInterfaces()
    {
        using var provider = BuildProvider();
        // Self-or-interfaces should pick interface when available.
        provider.GetService<AsTypesSelfOrInterfacesSample>().ShouldBeNull();

        var service = provider.GetRequiredService<IAsTypesSelfOrInterfacesSample>();
        service.Id.ShouldBe("self-or-interfaces");
    }

    [Test]
    public void AsTypesFallback_Default_UsesSelfOrInterfaces_WhenInterfacesExist()
    {
        using var provider = BuildProvider();
        // Default fallback should act like SelfOrInterfaces when interfaces exist.
        provider.GetService<AsTypesDefaultSample>().ShouldBeNull();

        var service = provider.GetRequiredService<IAsTypesDefaultSample>();
        service.Id.ShouldBe("default-interface");
    }

    [Test]
    public void AsTypesFallback_Default_UsesSelfOrInterfaces_WhenNoInterfaces()
    {
        using var provider = BuildProvider();
        // Default fallback should allow self registration when no interfaces exist.
        var self = provider.GetRequiredService<AsTypesDefaultSelfSample>();
        self.Id.ShouldBe("default-self");
    }

    [Test]
    public void DuplicateHandling_Add_AllowsMultipleRegistrations()
    {
        using var provider = BuildProvider();
        // DuplicateHandling.Add should retain all registrations.
        var servicesAll = provider.GetServices<IDuplicateAddSample>().ToList();

        servicesAll.Count.ShouldBe(2);
        servicesAll.Select(s => s.Id).ShouldBe(new[] { "add-first", "add-second" });
    }

    [Test]
    public void DuplicateHandling_Skip_SkipsDuplicates()
    {
        using var provider = BuildProvider();
        // DuplicateHandling.Skip should keep the first registration only.
        var servicesAll = provider.GetServices<IDuplicateSkipSample>().ToList();

        servicesAll.Count.ShouldBe(1);
        servicesAll[0].Id.ShouldBe("skip-first");
    }

    [Test]
    public void DuplicateHandling_Replace_ReplacesExisting()
    {
        using var provider = BuildProvider();
        // DuplicateHandling.Replace should keep the last registration.
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
        try
        {
            using var provider = BuildProvider("ThrowTest");
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

    private static ServiceProvider BuildProvider(string condition = TestCondition)
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(condition));
        return services.BuildServiceProvider();
    }

    internal interface IAsTypesSelfOnlySample
    {
        string Id { get; }
    }

    [Qudi(AsTypesFallback = AsTypesFallback.Self, When = [TestCondition])]
    internal sealed class AsTypesSelfOnlySample : IAsTypesSelfOnlySample
    {
        public string Id => "self-only";
    }

    internal interface IAsTypesInterfacesOnlySample
    {
        string Id { get; }
    }

    [Qudi(AsTypesFallback = AsTypesFallback.Interfaces, When = [TestCondition])]
    internal sealed class AsTypesInterfacesOnlySample : IAsTypesInterfacesOnlySample
    {
        public string Id => "interfaces-only";
    }

    internal interface IAsTypesSelfWithInterfacesSample
    {
        string Id { get; }
    }

    [Qudi(AsTypesFallback = AsTypesFallback.SelfWithInterfaces, When = [TestCondition])]
    internal sealed class AsTypesSelfWithInterfacesSample : IAsTypesSelfWithInterfacesSample
    {
        public string Id => "self-with-interface";
    }

    internal interface IAsTypesSelfOrInterfacesSample
    {
        string Id { get; }
    }

    [Qudi(AsTypesFallback = AsTypesFallback.SelfOrInterfaces, When = [TestCondition])]
    internal sealed class AsTypesSelfOrInterfacesSample : IAsTypesSelfOrInterfacesSample
    {
        public string Id => "self-or-interfaces";
    }

    internal interface IAsTypesDefaultSample
    {
        string Id { get; }
    }

    [Qudi(When = [TestCondition])]
    internal sealed class AsTypesDefaultSample : IAsTypesDefaultSample
    {
        public string Id => "default-interface";
    }

    [Qudi(When = [TestCondition])]
    internal sealed class AsTypesDefaultSelfSample
    {
        public string Id => "default-self";
    }

    internal interface IDuplicateAddSample
    {
        string Id { get; }
    }

    [Qudi(Duplicate = DuplicateHandling.Add, Order = 0, When = [TestCondition])]
    internal sealed class DuplicateAddSampleFirst : IDuplicateAddSample
    {
        public string Id => "add-first";
    }

    [Qudi(Duplicate = DuplicateHandling.Add, Order = 1, When = [TestCondition])]
    internal sealed class DuplicateAddSampleSecond : IDuplicateAddSample
    {
        public string Id => "add-second";
    }

    internal interface IDuplicateSkipSample
    {
        string Id { get; }
    }

    [Qudi(Duplicate = DuplicateHandling.Skip, Order = 0, When = [TestCondition])]
    internal sealed class DuplicateSkipSampleFirst : IDuplicateSkipSample
    {
        public string Id => "skip-first";
    }

    [Qudi(Duplicate = DuplicateHandling.Skip, Order = 1, When = [TestCondition])]
    internal sealed class DuplicateSkipSampleSecond : IDuplicateSkipSample
    {
        public string Id => "skip-second";
    }

    internal interface IDuplicateReplaceSample
    {
        string Id { get; }
    }

    [Qudi(Duplicate = DuplicateHandling.Replace, Order = 0, When = [TestCondition])]
    internal sealed class DuplicateReplaceSampleFirst : IDuplicateReplaceSample
    {
        public string Id => "replace-first";
    }

    [Qudi(Duplicate = DuplicateHandling.Replace, Order = 1, When = [TestCondition])]
    internal sealed class DuplicateReplaceSampleSecond : IDuplicateReplaceSample
    {
        public string Id => "replace-second";
    }

    internal interface IDuplicateThrowSample
    {
        string Id { get; }
    }

    [Qudi(Duplicate = DuplicateHandling.Throw, Order = 0, When = ["ThrowTest"])]
    internal sealed class DuplicateThrowSampleFirst : IDuplicateThrowSample
    {
        public string Id => "throw-first";
    }

    [Qudi(Duplicate = DuplicateHandling.Throw, Order = 1, When = ["ThrowTest"])]
    internal sealed class DuplicateThrowSampleSecond : IDuplicateThrowSample
    {
        public string Id => "throw-second";
    }
}
