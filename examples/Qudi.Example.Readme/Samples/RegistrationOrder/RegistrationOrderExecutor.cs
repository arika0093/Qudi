using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.RegistrationOrder;

public interface IService
{
    string GetName();
}

[DITransient(Order = -1)]
public class FirstService : IService
{
    public string GetName() => "First Service (Order = -1)";
}

[DITransient] // Order=0 by default
public class SecondService : IService
{
    public string GetName() => "Second Service (Order = 0)";
}

[DITransient(Order = 1)]
public class ThirdService : IService
{
    public string GetName() => "Third Service (Order = 1)";
}

[DISingleton(Export = true)]
public class RegistrationOrderExecutor(IEnumerable<IService> services) : ISampleExecutor
{
    public string Name => "Registration Order";
    public string Description => "Control registration order with Order property";
    public string Namespace => typeof(RegistrationOrderExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Services are registered in the specified order:");
        foreach (var service in services)
        {
            Console.WriteLine($"  - {service.GetName()}");
        }
    }
}
