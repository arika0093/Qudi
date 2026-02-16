using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class CompositeDecoratorOrderTests
{
    [Test]
    public void AppliesCompositeAndDecoratorByOrder_DecoratorAfterComposite()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IOrderedCompositeDecoratorServiceA>();

        service.Get().ShouldBe("D(A1|A2)");
    }

    [Test]
    public void AppliesCompositeAndDecoratorByOrder_DecoratorBeforeComposite()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IOrderedCompositeDecoratorServiceB>();

        service.Get().ShouldBe("D(B1)|D(B2)");
    }
}
