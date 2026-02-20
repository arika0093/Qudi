using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class DecoratorTests
{
    private const string TestCondition = nameof(DecoratorTests);

    [Test]
    public void AppliesDecoratorsInOrder()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService>();

        service.Send("hello").ShouldBe("D1(D2(hello))");
    }

    [Test]
    public void AppliesDecoratorToAllImplementationsInEnumeration()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<SendAll>();

        var results = sender.SendAllMessages("Test");

        results.Length.ShouldBe(2);
        results.ShouldContain("A:MESSAGE IS Test");
        results.ShouldContain("B:MESSAGE IS Test");
    }

    public interface IMessageService
    {
        string Send(string message);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class MessageService : IMessageService
    {
        public string Send(string message) => message;
    }

    [QudiDecorator(Order = 1, When = [TestCondition])]
    internal sealed partial class DecoratorOne(IMessageService inner) : IMessageService
    {
        public string Send(string message) => $"D1({inner.Send(message)})";
    }

    [QudiDecorator(Order = 2, When = [TestCondition])]
    internal sealed partial class DecoratorTwo(IMessageService inner) : IMessageService
    {
        public string Send(string message) => $"D2({inner.Send(message)})";
    }

    public interface ISend
    {
        string Msg(string a);
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class SendA : ISend
    {
        public string Msg(string a) => $"A:{a}";
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class SendB : ISend
    {
        public string Msg(string a) => $"B:{a}";
    }

    [QudiDecorator(When = [TestCondition])]
    internal sealed partial class SendDecorator(ISend send) : ISend
    {
        public string Msg(string a) => send.Msg($"MESSAGE IS {a}");
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class SendAll
    {
        private readonly IEnumerable<ISend> _sends;

        public SendAll(IEnumerable<ISend> sends)
        {
            _sends = sends;
        }

        public string[] SendAllMessages(string message)
        {
            return _sends.Select(s => s.Msg(message)).ToArray();
        }
    }
}
