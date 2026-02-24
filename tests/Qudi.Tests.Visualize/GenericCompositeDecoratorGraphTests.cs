using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Visualizer;
using Shouldly;
using TUnit;

namespace Qudi.Tests.Visualize
{
    public sealed partial class GenericCompositeDecoratorGraphTests
    {
        private const string Condition = nameof(GenericCompositeDecoratorGraphTests);

        [Test]
        public void GraphApproveJson() =>
            CompositeDecoratorGraphGenerate(QudiVisualizationFormat.Json);

        [Test]
        public void GraphApproveDot() =>
            CompositeDecoratorGraphGenerate(QudiVisualizationFormat.Dot);

        [Test]
        public void GraphApproveMermaid() =>
            CompositeDecoratorGraphGenerate(QudiVisualizationFormat.Mermaid);

        [Test]
        public void GraphApproveMarkdown() =>
            CompositeDecoratorGraphGenerate(QudiVisualizationFormat.Markdown);

        [Test]
        public void GraphApproveConsole()
        {
            var output = ConsoleOutputTestHelper.CaptureConsoleOutput(
                (mb, _) =>
                {
                    mb.AddFilter(r => r.When.Contains(Condition));
                    mb.SetCondition(Condition);
                    mb.EnableVisualizationOutput(opt =>
                    {
                        opt.ConsoleOutput = ConsoleDisplay.All;
                        opt.SuppressConsolePrompts = true;
                        opt.ConsoleEncoding = Encoding.UTF8;
                    });
                },
                provider => _ = provider.GetRequiredService<GraphRoot>()
            );

            output.ShouldMatchApproved(c =>
                c.SubFolder("export").WithFileExtension(".console.txt").NoDiff()
            );
        }

        private static void CompositeDecoratorGraphGenerate(QudiVisualizationFormat format)
        {
            var rootDir = Path.Combine(
                Path.GetTempPath(),
                "Qudi.Visualize.Tests",
                Guid.NewGuid().ToString("N")
            );

            var ext = format.ToExtension();
            var path = Path.Combine(rootDir, $"graph.{ext}");
            var services = new ServiceCollection();
            services.AddQudiServices(conf =>
            {
                conf.AddFilter(r => r.When.Contains(Condition));
                conf.SetCondition(Condition);
                conf.EnableVisualizationOutput(opt =>
                {
                    opt.ConsoleOutput = ConsoleDisplay.None;
                    opt.AddOutput(path, format);
                });
            });

            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<GraphRoot>();

            ApproveFile(path, ext);
        }

        private static void ApproveFile(string path, string ext)
        {
            File.Exists(path).ShouldBeTrue();
            var content = File.ReadAllText(path);
            content.ShouldMatchApproved(c => c.SubFolder("export").WithFileExtension($".{ext}"));
        }

        [DITransient(Export = true, When = [Condition])]
        internal sealed class GraphRoot
        {
            public GraphRoot(ComponentValidator validator)
            {
                Validator = validator;
            }

            public ComponentValidator Validator { get; }
        }

        [DITransient(When = [Condition])]
        internal sealed class ComponentValidator(IComponentValidator<IComponent> validator)
        {
            public bool Validate(IComponent component) => validator.Validate(component);
        }

        [QudiDispatch(When = [Condition])]
        internal sealed partial class ComponentValidatorDispatcher
            : IComponentValidator<IComponent>;

        [QudiDecorator(When = [Condition])]
        internal sealed partial class ComponentValidatorDecorator(
            IComponentValidator<IComponent> decorated
        ) : IComponentValidator<IComponent>
        {
            public bool Validate(IComponent component) => decorated.Validate(component);
        }

        [DITransient(When = [Condition])]
        internal sealed class NullComponentValidator<T> : IComponentValidator<T>
            where T : IComponent
        {
            public bool Validate(T component) => true;
        }

        [DITransient(When = [Condition])]
        internal sealed class BatteryValidator : IComponentValidator<Battery>
        {
            public bool Validate(Battery component) => true;
        }

        [DITransient(When = [Condition])]
        internal sealed class ScreenValidator : IComponentValidator<Screen>
        {
            public bool Validate(Screen component) => true;
        }

        internal interface IComponentValidator<T>
            where T : IComponent
        {
            bool Validate(T component);
        }

        internal interface IComponent
        {
            string Name { get; }
        }

        internal sealed class Battery : IComponent
        {
            public string Name => "Battery";
        }

        internal sealed class Screen : IComponent
        {
            public string Name => "Screen";
        }

        internal sealed class Keyboard : IComponent
        {
            public string Name => "Keyboard";
        }
    }
}
