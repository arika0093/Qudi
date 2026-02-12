using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class GenericRegistrationTests
{
    [Test]
    public void RegistersOpenGenericServices()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IGenericRepository<string>>();
        repo.ValueType.ShouldBe(typeof(string));

        var concrete = provider.GetRequiredService<GenericRepository<string>>();
        concrete.ValueType.ShouldBe(typeof(string));
    }
}

public interface IGenericRepository<T>
{
    System.Type ValueType { get; }
}

[DITransient]
public class GenericRepository<T> : IGenericRepository<T>
{
    public System.Type ValueType => typeof(T);
}
