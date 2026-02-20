using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class CompositeTests
{
    private const string TestCondition = nameof(CompositeTests);

    [Test]
    public void CompositeCallsAllServices()
    {
        using var provider = BuildProvider();
        var composite = provider.GetRequiredService<INotificationService>();

        var result = CompositeNotificationService.Messages;
        result.Clear();

        composite.Notify("test message");

        result.Count.ShouldBe(2);
        result.ShouldContain("Email: test message");
        result.ShouldContain("SMS: test message");
    }

    [Test]
    public void CompositeWithPartialClass()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IMessageService2>();

        var result = CompositeMessageService.Messages;
        result.Clear();

        service.Send("test");

        result.Count.ShouldBe(2);
        result.ShouldContain("ServiceA: test");
        result.ShouldContain("ServiceB: test");
    }

    [Test]
    public void CompositeWithBoolReturnAggregatesWithAnd()
    {
        using var provider = BuildProvider();
        var validator = provider.GetRequiredService<IValidationService>();

        // Valid input (length > 0 AND all letters)
        validator.Validate("abc").ShouldBeTrue();

        // Invalid: empty string (length check fails)
        validator.Validate("").ShouldBeFalse();

        // Invalid: contains numbers (alpha check fails)
        validator.Validate("abc123").ShouldBeFalse();
    }

    [Test]
    public void CompositeWithEnumerableReturnCombinesResults()
    {
        using var provider = BuildProvider();
        var dataProvider = provider.GetRequiredService<IDataProvider>();

        var data = dataProvider.GetData().ToList();
        data.ShouldNotBeNull();
        data.Count.ShouldBe(4); // A1, A2, B1, B2
        data.ShouldContain("A1");
        data.ShouldContain("A2");
        data.ShouldContain("B1");
        data.ShouldContain("B2");
    }

    [Test]
    public void CompositeMethodAttributeOverridesResultHandling()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<ICompositeMethodService>();

        service.AllCheck().ShouldBeFalse();
        service.AnyCheck().ShouldBeTrue();
    }

    [Test]
    public void CompositeUnsupportedMembersThrow()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<ICompositeMethodService>();

        Should.Throw<NotSupportedException>(() => service.UnsupportedMethod());
        Should.Throw<NotSupportedException>(() => _ = service.UnsupportedProperty);
    }

    [Test]
    public async Task CompositeWithTaskReturnAwaitsAll()
    {
        using var provider = BuildProvider();
        var asyncService = provider.GetRequiredService<IAsyncService>();

        CompositeAsyncService.ProcessedItems.Clear();

        await asyncService.ProcessAsync("test");

        CompositeAsyncService.ProcessedItems.Count.ShouldBe(2);
        CompositeAsyncService.ProcessedItems.ShouldContain("A:test");
        CompositeAsyncService.ProcessedItems.ShouldContain("B:test");
    }

    [Test]
    public async Task CompositeWithSequentialTaskExecutesInOrder()
    {
        using var provider = BuildProvider();
        var sequentialService = provider.GetRequiredService<ISequentialAsyncService>();

        CompositeSequentialAsyncService.ExecutionOrder.Clear();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await sequentialService.ExecuteAsync("test");
        stopwatch.Stop();

        // Sequential execution should take at least 100ms (50ms * 2)
        // whereas parallel would take only 50ms
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(90);

        CompositeSequentialAsyncService.ExecutionOrder.Count.ShouldBe(2);

        // Parse timestamps and verify sequential order
        var aTicks = long.Parse(CompositeSequentialAsyncService.ExecutionOrder[0].Split(':')[2]);
        var bTicks = long.Parse(CompositeSequentialAsyncService.ExecutionOrder[1].Split(':')[2]);

        // B should execute after A (timestamp should be later)
        bTicks.ShouldBeGreaterThan(aTicks);
    }

    [Test]
    public void CompositeWithCustomAggregatorCombinesFlags()
    {
        using var provider = BuildProvider();
        var flagService = provider.GetRequiredService<IFlagService>();

        var flags = flagService.GetFlags();
        flags.ShouldBe(AccessFlags.Read | AccessFlags.Write);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));
        return services.BuildServiceProvider();
    }

    // Simple composite example
    public interface INotificationService
    {
        void Notify(string message);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class EmailNotificationService : INotificationService
    {
        public void Notify(string message)
        {
            CompositeNotificationService.Messages.Add($"Email: {message}");
        }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class SmsNotificationService : INotificationService
    {
        public void Notify(string message)
        {
            CompositeNotificationService.Messages.Add($"SMS: {message}");
        }
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed class CompositeNotificationService(
        System.Collections.Generic.IEnumerable<INotificationService> innerServices
    ) : INotificationService
    {
        public static readonly System.Collections.Generic.List<string> Messages = new();

        public void Notify(string message)
        {
            foreach (var service in innerServices)
            {
                service.Notify(message);
            }
        }
    }

    // Partial class composite example
    public interface IMessageService2
    {
        void Send(string message);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class MessageServiceA : IMessageService2
    {
        public void Send(string message)
        {
            CompositeMessageService.Messages.Add($"ServiceA: {message}");
        }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class MessageServiceB : IMessageService2
    {
        public void Send(string message)
        {
            CompositeMessageService.Messages.Add($"ServiceB: {message}");
        }
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeMessageService(
        System.Collections.Generic.IEnumerable<IMessageService2> innerServices
    ) : IMessageService2
    {
        public static readonly System.Collections.Generic.List<string> Messages = new();
    }

    // Test result aggregation with bool return type
    public interface IValidationService
    {
        bool Validate(string input);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class LengthValidator : IValidationService
    {
        public bool Validate(string input) => input.Length > 0;
    }

    [DITransient(When = [TestCondition])]
    internal sealed class AlphaValidator : IValidationService
    {
        public bool Validate(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
            foreach (var c in input)
            {
                if (!char.IsLetter(c))
                    return false;
            }
            return true;
        }
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeValidationService(
        System.Collections.Generic.IEnumerable<IValidationService> innerServices
    ) : IValidationService;

    // Test result aggregation with IEnumerable return type
    public interface IDataProvider
    {
        System.Collections.Generic.IEnumerable<string> GetData();
    }

    [DITransient(When = [TestCondition])]
    internal sealed class DataProviderA : IDataProvider
    {
        public System.Collections.Generic.IEnumerable<string> GetData() => new[] { "A1", "A2" };
    }

    [DITransient(When = [TestCondition])]
    internal sealed class DataProviderB : IDataProvider
    {
        public System.Collections.Generic.IEnumerable<string> GetData() => new[] { "B1", "B2" };
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeDataProvider(
        System.Collections.Generic.IEnumerable<IDataProvider> innerServices
    ) : IDataProvider;

    public interface ICompositeMethodService
    {
        bool AllCheck();
        bool AnyCheck();
        int UnsupportedMethod();
        string UnsupportedProperty { get; }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class CompositeMethodServiceTrue : ICompositeMethodService
    {
        public bool AllCheck() => true;

        public bool AnyCheck() => false;

        public int UnsupportedMethod() => 1;

        public string UnsupportedProperty => "true";
    }

    [DITransient(When = [TestCondition])]
    internal sealed class CompositeMethodServiceFalse : ICompositeMethodService
    {
        public bool AllCheck() => false;

        public bool AnyCheck() => true;

        public int UnsupportedMethod() => 2;

        public string UnsupportedProperty => "false";
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeMethodService(
        System.Collections.Generic.IEnumerable<ICompositeMethodService> innerServices
    ) : ICompositeMethodService
    {
        [CompositeMethod(Result = CompositeResult.All)]
        public partial bool AllCheck();

        [CompositeMethod(Result = CompositeResult.Any)]
        public partial bool AnyCheck();
    }

    // Test result aggregation with Task return type
    public interface IAsyncService
    {
        Task ProcessAsync(string input);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class AsyncServiceA : IAsyncService
    {
        public async Task ProcessAsync(string input)
        {
            await Task.Delay(10);
            CompositeAsyncService.ProcessedItems.Add($"A:{input}");
        }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class AsyncServiceB : IAsyncService
    {
        public async Task ProcessAsync(string input)
        {
            await Task.Delay(10);
            CompositeAsyncService.ProcessedItems.Add($"B:{input}");
        }
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeAsyncService(
        System.Collections.Generic.IEnumerable<IAsyncService> innerServices
    ) : IAsyncService
    {
        public static readonly System.Collections.Concurrent.ConcurrentBag<string> ProcessedItems =
            new();
    }

    // Test Sequential execution
    public interface ISequentialAsyncService
    {
        Task ExecuteAsync(string input);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class SequentialServiceA : ISequentialAsyncService
    {
        public async Task ExecuteAsync(string input)
        {
            await Task.Delay(50);
            CompositeSequentialAsyncService.ExecutionOrder.Add(
                $"A:{input}:{System.DateTime.UtcNow.Ticks}"
            );
        }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class SequentialServiceB : ISequentialAsyncService
    {
        public async Task ExecuteAsync(string input)
        {
            await Task.Delay(50);
            CompositeSequentialAsyncService.ExecutionOrder.Add(
                $"B:{input}:{System.DateTime.UtcNow.Ticks}"
            );
        }
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeSequentialAsyncService(
        System.Collections.Generic.IEnumerable<ISequentialAsyncService> innerServices
    ) : ISequentialAsyncService
    {
        public static readonly System.Collections.Generic.List<string> ExecutionOrder = new();

        public partial Task ExecuteAsync(string input);
    }

    [System.Flags]
    public enum AccessFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
    }

    public interface IFlagService
    {
        AccessFlags GetFlags();
    }

    [DITransient(When = [TestCondition])]
    internal sealed class FlagServiceRead : IFlagService
    {
        public AccessFlags GetFlags() => AccessFlags.Read;
    }

    [DITransient(When = [TestCondition])]
    internal sealed class FlagServiceWrite : IFlagService
    {
        public AccessFlags GetFlags() => AccessFlags.Write;
    }

    [QudiComposite(When = [TestCondition])]
    internal sealed partial class CompositeFlagService(
        System.Collections.Generic.IEnumerable<IFlagService> innerServices
    ) : IFlagService
    {
        [CompositeMethod(ResultAggregator = nameof(CombineFlags))]
        public partial AccessFlags GetFlags();

        private static AccessFlags CombineFlags(AccessFlags left, AccessFlags right) =>
            left | right;
    }
}
