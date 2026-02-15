using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.ConditionalRegistration;

public interface IPaymentService
{
    void ProcessPayment(decimal amount);
}

[DITransient(When = [Condition.Development])]
public class MockPaymentService : IPaymentService
{
    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"💳 [Mock] Processed payment of {amount:C}");
    }
}

[DITransient(When = [Condition.Production])]
public class RealPaymentService : IPaymentService
{
    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"💰 [Real] Processing payment of {amount:C}...");
        // Actual payment processing logic would go here
    }
}

[DITransient(Export = true)]
public class ConditionalRegistrationExecutor(IPaymentService? paymentService) : ISampleExecutor
{
    public string Name => "Conditional Registration";
    public string Description => "Register services conditionally based on environment";
    public string Namespace => typeof(ConditionalRegistrationExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Note: This sample shows conditional registration.");
        Console.WriteLine(
            "Set ASPNETCORE_ENVIRONMENT to 'Development' or 'Production' to see different implementations."
        );
        Console.WriteLine();

        if (paymentService != null)
        {
            paymentService.ProcessPayment(99.99m);
        }
        else
        {
            Console.WriteLine(
                "⚠️  No payment service registered. Set environment variable to register one."
            );
        }
    }
}
