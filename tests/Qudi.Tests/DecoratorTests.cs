using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DecoratorTests
{
    [Test]
    public void AppliesDecoratorsInOrder()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService>();

        service.Send("hello").ShouldBe("D2(D1(hello))");
    }
}
