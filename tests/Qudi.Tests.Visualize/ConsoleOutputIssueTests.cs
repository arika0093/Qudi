using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Visualizer;
using TUnit;

namespace Qudi.Tests.Visualize;

public sealed class ConsoleOutputIssueTests
{
    private const string MissingCondition = "ConsoleOutputMissing";
    private const string CycleCondition = "ConsoleOutputCycle";
    private const string LifetimeCondition = "ConsoleOutputLifetime";

    [Test]
    public void ApproveConsoleMissing()
    {
        var output = ConsoleOutputTestHelper.CaptureConsoleOutput(
            (mb, _) =>
            {
                mb.AddFilter(r => r.When.Contains(MissingCondition));
                mb.SetCondition(MissingCondition);
                mb.EnableVisualizationOutput(opt =>
                {
                    opt.ConsoleOutput = ConsoleDisplay.All;
                    opt.SuppressConsolePrompts = true;
                    opt.ConsoleEncoding = System.Text.Encoding.UTF8;
                });
            },
            _ => { }
        );

        output.ShouldMatchApproved(c =>
            c.SubFolder("export").WithFileExtension(".console.txt").NoDiff()
        );
    }

    [Test]
    public void ApproveConsoleCycle()
    {
        var output = ConsoleOutputTestHelper.CaptureConsoleOutput(
            (mb, _) =>
            {
                mb.AddFilter(r => r.When.Contains(CycleCondition));
                mb.SetCondition(CycleCondition);
                mb.EnableVisualizationOutput(opt =>
                {
                    opt.ConsoleOutput = ConsoleDisplay.All;
                    opt.SuppressConsolePrompts = true;
                    opt.ConsoleEncoding = System.Text.Encoding.UTF8;
                });
            },
            _ => { }
        );

        output.ShouldMatchApproved(c =>
            c.SubFolder("export").WithFileExtension(".console.txt").NoDiff()
        );
    }

    [Test]
    public void ApproveConsoleLifetimeWarning()
    {
        var output = ConsoleOutputTestHelper.CaptureConsoleOutput(
            (mb, _) =>
            {
                mb.AddFilter(r => r.When.Contains(LifetimeCondition));
                mb.SetCondition(LifetimeCondition);
                mb.EnableVisualizationOutput(opt =>
                {
                    opt.ConsoleOutput = ConsoleDisplay.All;
                    opt.SuppressConsolePrompts = true;
                    opt.ConsoleEncoding = System.Text.Encoding.UTF8;
                });
            },
            _ => { }
        );

        output.ShouldMatchApproved(c =>
            c.SubFolder("export").WithFileExtension(".console.txt").NoDiff()
        );
    }

    [DITransient(Export = true, When = [MissingCondition])]
    internal sealed class MissingRoot(IMissingDependency dependency);

    internal interface IMissingDependency;

    [DITransient(Export = true, When = [CycleCondition])]
    internal sealed class CycleRoot(CycleA a);

    [DITransient(When = [CycleCondition])]
    internal sealed class CycleA(CycleB b);

    [DITransient(When = [CycleCondition])]
    internal sealed class CycleB(CycleC c);

    [DITransient(When = [CycleCondition])]
    internal sealed class CycleC(CycleA a);

    [DISingleton(Export = true, When = [LifetimeCondition])]
    internal sealed class LifetimeRoot(TransientDependency dependency);

    [DITransient(When = [LifetimeCondition])]
    internal sealed class TransientDependency;
}
