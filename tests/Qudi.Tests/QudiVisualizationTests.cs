using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi.Tests;

public sealed class QudiVisualizationTests
{
    [Test]
    public void WriteConsoleVisualizationOutput()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.EnableVisualizationOutput(options => options.EnableConsoleOutput = true);
        });

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IQudiAttributeSample>();
        first.Value.ShouldBe("by-qudi-attribute");
    }


    [Test]
    public void WritesVisualizationJson()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "qudi-visualizer-" + Guid.NewGuid().ToString("N") + ".json"
        );

        try
        {
            var services = new ServiceCollection();
            services.AddQudiServices(conf =>
                conf.EnableVisualizationOutput(tempPath, options => options.EnableConsoleOutput = false)
            );

            File.Exists(tempPath).ShouldBeTrue();
            var json = File.ReadAllText(tempPath);
            json.ShouldContain("\"Report\"");
            json.ShouldContain("\"Summary\"");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
