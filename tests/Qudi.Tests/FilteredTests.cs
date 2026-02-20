using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests
{
    public sealed class FilteredTests
    {
        private const string TestCondition = nameof(FilteredTests);

        [Test]
        public void RegistersServicesWithFilterApplied()
        {
            var services = new ServiceCollection();
            services.AddQudiServices(conf =>
            {
                conf.SetCondition(TestCondition);
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
    using Qudi;

    public interface IFilteredService
    {
        string GetValue();
    }

    [DISingleton(When = [nameof(Qudi.Tests.FilteredTests), nameof(Qudi.Tests.CustomizationAddServiceTests)])]
    public class FilteredService : IFilteredService
    {
        public string GetValue() => "I am filtered out service";
    }
}

namespace Qudi.Tests.NotFiltered
{
    using Qudi;

    [DISingleton(When = [nameof(Qudi.Tests.FilteredTests), nameof(Qudi.Tests.CustomizationAddServiceTests)])]
    public class NotFilteredService : Qudi.Tests.FilteredOut.IFilteredService
    {
        public string GetValue() => "I am not filtered out service";
    }
}
