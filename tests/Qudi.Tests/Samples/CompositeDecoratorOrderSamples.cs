using System.Collections.Generic;
using System.Linq;
using Qudi;

namespace Qudi.Tests;

public interface IOrderedCompositeDecoratorServiceA
{
    string Get();
}

[DITransient(Order = -1)]
public sealed class OrderedCompositeDecoratorServiceA1 : IOrderedCompositeDecoratorServiceA
{
    public string Get() => "A1";
}

[DITransient(Order = 1)]
public sealed class OrderedCompositeDecoratorServiceA2 : IOrderedCompositeDecoratorServiceA
{
    public string Get() => "A2";
}

[QudiComposite(Order = 0)]
public sealed partial class OrderedCompositeServiceA(
    IEnumerable<IOrderedCompositeDecoratorServiceA> innerServices
) : IOrderedCompositeDecoratorServiceA
{
    public string Get() => string.Join("|", innerServices.Select(x => x.Get()));
}

[QudiDecorator(Order = 1)]
public sealed class OrderedDecoratorServiceA(IOrderedCompositeDecoratorServiceA innerService)
    : IOrderedCompositeDecoratorServiceA
{
    public string Get() => $"D({innerService.Get()})";
}

public interface IOrderedCompositeDecoratorServiceB
{
    string Get();
}

[DITransient(Order = -1)]
public sealed class OrderedCompositeDecoratorServiceB1 : IOrderedCompositeDecoratorServiceB
{
    public string Get() => "B1";
}

[DITransient(Order = 1)]
public sealed class OrderedCompositeDecoratorServiceB2 : IOrderedCompositeDecoratorServiceB
{
    public string Get() => "B2";
}

[QudiDecorator(Order = -1)]
public sealed class OrderedDecoratorServiceB(IOrderedCompositeDecoratorServiceB innerService)
    : IOrderedCompositeDecoratorServiceB
{
    public string Get() => $"D({innerService.Get()})";
}

[QudiComposite(Order = 0)]
public sealed partial class OrderedCompositeServiceB(
    IEnumerable<IOrderedCompositeDecoratorServiceB> innerServices
) : IOrderedCompositeDecoratorServiceB
{
    public string Get() => string.Join("|", innerServices.Select(x => x.Get()));
}
