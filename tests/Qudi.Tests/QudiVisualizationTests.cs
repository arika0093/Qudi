using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class QudiVisualizationTests
{
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
