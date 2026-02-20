using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class GenericCompositeTests
{
    [Test]
    public void GenericCompositeDispatchResolvesBaseInterface()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        using var provider = services.BuildServiceProvider();

        var battery1 = new Battery
        {
            Name = "Battery 1",
            Capacity = 6000,
            Voltage = 4,
        };
        var battery2 = new Battery
        {
            Name = "Battery 2",
            Capacity = 4000,
            Voltage = 2,
        };
        var screen = new Screen { Name = "Screen", Size = 12 };
        var keyboard = new Keyboard { Name = "Keyboard", Keys = 104 };

        // Resolve the base interface via dispatch composite.
        var validator = provider.GetRequiredService<IComponentValidator<IComponent>>();
        var componentValidator = provider.GetRequiredService<ComponentValidator>();

        validator.Validate(battery1).ShouldBeTrue();
        validator.Validate(battery2).ShouldBeFalse();
        validator.Validate(screen).ShouldBeTrue();
        validator.Validate(keyboard).ShouldBeTrue();

        componentValidator.Validate(battery1).ShouldBeTrue();
        componentValidator.Validate(battery2).ShouldBeFalse();
        componentValidator.Validate(screen).ShouldBeTrue();
        componentValidator.Validate(keyboard).ShouldBeTrue();
    }

    [Test]
    public void QudiDispatchWithMultipleFalseResolvesSingleService()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<ISingleComponentValidator<ISingleComponent>>();

        validator.Validate(new SingleDevice()).ShouldBeTrue();
    }

    [QudiDispatch(When = [TestCondition])]
    public sealed partial class ComponentValidatorDispatcher : IComponentValidator<IComponent>;

    [QudiDispatch(Target = typeof(ISingleComponent), Multiple = false, When = [TestCondition])]
    public sealed partial class SingleComponentValidatorDispatcher
        : ISingleComponentValidator<ISingleComponent>;

    [DITransient(When = [TestCondition])]
    public sealed class ComponentValidator(IComponentValidator<IComponent> validator)
    {
        public bool Validate(IComponent component) => validator.Validate(component);
    }

    public interface ISingleComponent { }

    public sealed class SingleDevice : ISingleComponent { }

    public interface ISingleComponentValidator<T>
        where T : ISingleComponent
    {
        bool Validate(T value);
    }

    [DITransient(When = [TestCondition])]
    public sealed class SingleDeviceValidator : ISingleComponentValidator<SingleDevice>
    {
        public bool Validate(SingleDevice value) => true;
    }
}
