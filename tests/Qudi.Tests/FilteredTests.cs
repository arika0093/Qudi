using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests
{
    public sealed class FilteredTests
    {
        [Test]
        public void RegistersServicesWithFilterApplied()
        {
            var services = new ServiceCollection();
            services.AddQudiServices(conf =>
            {
                conf.AddFilter(registration => !registration.Namespace.Contains("FilteredOut"));
            });
            var provider = services.BuildServiceProvider();
            var filteredServices = provider.GetServices<FilteredOut.IFilteredService>().ToList();
            filteredServices.Count.ShouldBe(1);
            filteredServices.First().GetValue().ShouldBe("I am not filtered out service");
        }
    }
}

namespace Qudi.Tests.FilteredOut
{
    public interface IFilteredService
    {
        string GetValue();
    }

    [DISingleton]
    public class FilteredService : IFilteredService
    {
        public string GetValue() => "I am filtered out service";
    }
}

namespace Qudi.Tests.NotFiltered
{
    [DISingleton]
    public class NotFilteredService : Qudi.Tests.FilteredOut.IFilteredService
    {
        public string GetValue() => "I am not filtered out service";
    }
}
