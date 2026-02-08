using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class QudiAttributeRegistrationTests
{
    [Test]
    public void RegistersServicesMarkedWithQudiAttribute()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IQudiAttributeSample>();
        var second = provider.GetRequiredService<IQudiAttributeSample>();

        first.Value.ShouldBe("by-qudi-attribute");
        ReferenceEquals(first, second).ShouldBeTrue();
    }
}
