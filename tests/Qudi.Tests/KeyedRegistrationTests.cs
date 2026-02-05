using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class KeyedRegistrationTests
{
    [Test]
    public void RegistersKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IKeyedSample>("alpha");

        service.Value.ShouldBe("alpha");
        provider.GetService<IKeyedSample>().ShouldBeNull();
    }
}
