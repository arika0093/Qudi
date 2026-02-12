using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DecoratorNestedInterfaceTests
{
    [Test]
    public void AutoImplementsNestedInterfaces()
    {
        var service = new NestedInterfaceService();
        var decorator = new NestedInterfaceDecorator(service);

        ((IFoo)decorator).Foo().ShouldBe("decorated foo");
        ((IBar)decorator).Bar().ShouldBe("bar");
        ((IBar_A)decorator).BarA().ShouldBe("bar-a");
        ((IBar_B)decorator).BarB().ShouldBe("bar-b");
        ((IBuz)decorator).Buz().ShouldBe("buz");
        ((IFooBar)decorator).FooBar().ShouldBe("foo-bar");
    }
}
