using Qudi;

namespace Qudi.Tests;

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

[DITransient]
public sealed class NestedInterfaceService : IFoo, IBar, IBuz
{
    public string Foo() => "foo";

    public string Bar() => "bar";

    public string BarA() => "bar-a";

    public string BarB() => "bar-b";

    public string Buz() => "buz";

    public string FooBar() => "foo-bar";
}

[QudiDecorator]
public sealed partial class NestedInterfaceDecorator(NestedInterfaceService innerService)
    : IFoo,
        IBar,
        IBuz
{
    public string Foo() => $"decorated {innerService.Foo()}";
}
