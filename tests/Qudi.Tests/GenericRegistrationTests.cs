using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class GenericRegistrationTests
{
    private const string TestCondition = nameof(GenericRegistrationTests);

    [Test]
    public void RegistersOpenGenericServices()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IGenericRepository<string>>();
        repo.ValueType.ShouldBe(typeof(string));

        provider.GetService<GenericRepository<string>>().ShouldBeNull();
    }

    internal interface IGenericRepository<T>
    {
        System.Type ValueType { get; }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class GenericRepository<T> : IGenericRepository<T>
    {
        public System.Type ValueType => typeof(T);
    }
}
