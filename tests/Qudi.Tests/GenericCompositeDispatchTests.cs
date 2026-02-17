using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class GenericCompositeTests
{
    [Test]
    public void GenericCompositeDispatchResolvesBaseInterface()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

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

    [QudiComposite]
    public partial class ComponentValidatorDispatcher<T> : IComponentValidator<T>
        where T : IComponent;

    [DITransient]
    public class ComponentValidator(IComponentValidator<IComponent> validator)
    {
        public bool Validate(IComponent component) => validator.Validate(component);
    }
}
