using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class CompositeDecoratorOrderTests
{
    private const string TestCondition = nameof(CompositeDecoratorOrderTests);

    [Test]
    public void AppliesCompositeAndDecoratorByOrder_DecoratorAfterComposite()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IOrderedCompositeDecoratorServiceA>();

        service.Get().ShouldBe("D(A1)|D(A2)");
    }

    [Test]
    public void AppliesCompositeAndDecoratorByOrder_DecoratorBeforeComposite()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IOrderedCompositeDecoratorServiceB>();

        service.Get().ShouldBe("D(B1|B2)");
    }

    public interface IOrderedCompositeDecoratorServiceA
    {
        string Get();
    }

    [DITransient(Order = -1, When = [TestCondition])]
    internal sealed class OrderedCompositeDecoratorServiceA1 : IOrderedCompositeDecoratorServiceA
    {
        public string Get() => "A1";
    }

    [DITransient(Order = 1, When = [TestCondition])]
    internal sealed class OrderedCompositeDecoratorServiceA2 : IOrderedCompositeDecoratorServiceA
    {
        public string Get() => "A2";
    }

    [QudiComposite(Order = 0, When = [TestCondition])]
    internal sealed partial class OrderedCompositeServiceA(
        IEnumerable<IOrderedCompositeDecoratorServiceA> innerServices
    ) : IOrderedCompositeDecoratorServiceA
    {
        public string Get() => string.Join("|", innerServices.Select(x => x.Get()));
    }

    [QudiDecorator(Order = 1, When = [TestCondition])]
    internal sealed class OrderedDecoratorServiceA(IOrderedCompositeDecoratorServiceA innerService)
        : IOrderedCompositeDecoratorServiceA
    {
        public string Get() => $"D({innerService.Get()})";
    }

    public interface IOrderedCompositeDecoratorServiceB
    {
        string Get();
    }

    [DITransient(Order = -1, When = [TestCondition])]
    internal sealed class OrderedCompositeDecoratorServiceB1 : IOrderedCompositeDecoratorServiceB
    {
        public string Get() => "B1";
    }

    [DITransient(Order = 1, When = [TestCondition])]
    internal sealed class OrderedCompositeDecoratorServiceB2 : IOrderedCompositeDecoratorServiceB
    {
        public string Get() => "B2";
    }

    [QudiDecorator(Order = -1, When = [TestCondition])]
    internal sealed class OrderedDecoratorServiceB(IOrderedCompositeDecoratorServiceB innerService)
        : IOrderedCompositeDecoratorServiceB
    {
        public string Get() => $"D({innerService.Get()})";
    }

    [QudiComposite(Order = 0, When = [TestCondition])]
    internal sealed partial class OrderedCompositeServiceB(
        IEnumerable<IOrderedCompositeDecoratorServiceB> innerServices
    ) : IOrderedCompositeDecoratorServiceB
    {
        public string Get() => string.Join("|", innerServices.Select(x => x.Get()));
    }
}
