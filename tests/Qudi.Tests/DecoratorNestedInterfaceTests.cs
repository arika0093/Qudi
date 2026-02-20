using System.Collections.Generic;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class DecoratorNestedInterfaceTests
{
    private const string TestCondition = nameof(DecoratorNestedInterfaceTests);

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

    public interface IFoo
    {
        string Foo();
    }

    public interface IBuz
    {
        string Buz();
    }

    public interface IFooBar
    {
        string FooBar();
    }

    public interface IBar_A : IBuz, IFooBar
    {
        string BarA();
    }

    public interface IBar_B
    {
        string BarB();
    }

    public interface IBar : IBar_A, IBar_B
    {
        string Bar();
    }

    [DITransient(When = [TestCondition])]
    internal sealed class NestedInterfaceService : IFoo, IBar, IBuz
    {
        public string Foo() => "foo";

        public string Bar() => "bar";

        public string BarA() => "bar-a";

        public string BarB() => "bar-b";

        public string Buz() => "buz";

        public string FooBar() => "foo-bar";
    }

    [QudiDecorator(When = [TestCondition])]
    internal sealed partial class NestedInterfaceDecorator(NestedInterfaceService innerService)
        : IFoo,
            IBar,
            IBuz
    {
        public string Foo() => $"decorated {innerService.Foo()}";
    }

    [QudiDecorator(UseIntercept = true, When = [TestCondition])]
    internal sealed partial class NestedInterfaceIntercept(NestedInterfaceService innerService) : IFoo
    {
        public List<string> Calls { get; } = [];

        public IEnumerable<bool> Intercept(string methodName, object?[] parameters)
        {
            Calls.Add(methodName);
            yield return true;
        }
    }
}
