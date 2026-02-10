using Microsoft.CodeAnalysis;

namespace Qudi.Generator.Registration;

internal static class RegistrationAttrGenerator
{
    /// <summary>
    /// Adds the Qudi attribute codes to the generator context.
    /// </summary>
    public static void AddQudiAttributeCodes(
        this IncrementalGeneratorPostInitializationContext context
    )
    {
        context.AddSource("Qudi.Attributes.g.cs", QudiAttributeCode);
    }

    private const string AttributeClassUsage =
        "[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]";

    private const string QudiAttributeCode = $$"""
        {{CodeTemplateContents.CommonGeneratedHeader}}
        using System;

        {{CodeTemplateContents.EmbeddedAttributeSource}}

        namespace Qudi
        {
            /// <summary>
            /// Full configuration attribute for Qudi registration.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public class QudiAttribute : Attribute
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="QudiAttribute"/> class.
                /// </summary>
                public QudiAttribute() { }

                /// <summary>
                /// The lifetime of the registration.
                /// </summary>
                public string? Lifetime { get; set; }

                /// <summary>
                /// Trigger registration only in specific conditions.
                /// </summary>
                public string[]? When { get; set; }

                /// <summary>
                /// The types to register as.
                /// It is automatically identified, but you can also specify it explicitly
                /// </summary>
                public Type[]? AsTypes { get; set; }

                /// <summary>
                /// Make this class accessible from other projects?
                /// </summary>
                public bool UsePublic { get; set; }

                /// <summary>
                /// The key for keyed registrations. If null, no key is used.
                /// </summary>
                public object? Key { get; set; }

                /// <summary>
                /// The order of registration. Higher numbers are registered later.
                /// </summary>
                public int Order { get; set; }

                /// <summary>
                /// Whether this registration is a decorator.
                /// </summary>
                public bool MarkAsDecorator { get; set; }

                /// <summary>
                /// Whether this registration is a strategy.
                /// </summary>
                public bool MarkAsStrategy { get; set; }
            }

            /// <summary>
            /// Shorthand attribute for singleton lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DISingletonAttribute : QudiAttribute {}

            /// <summary>
            /// Shorthand attribute for transient lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DITransientAttribute : QudiAttribute {}

            /// <summary>
            /// Shorthand attribute for scoped lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DIScopedAttribute : QudiAttribute {}

            /// <summary>
            /// Shorthand attribute for decorator registration.
            /// Lifetime is not configurable for decorators.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class QudiDecoratorAttribute : Attribute
            {
                /// <summary>
                /// Trigger registration only in specific conditions.
                /// </summary>
                public string[]? When { get; set; }

                /// <summary>
                /// The types to register the service as.
                /// It is automatically identified, but you can also specify it explicitly
                /// </summary>
                public Type[]? AsTypes { get; set; }

                /// <summary>
                /// Make this class accessible from other projects?
                /// </summary>
                public bool UsePublic { get; set; }

                /// <summary>
                /// The key for keyed registrations. If null, no key is used.
                /// </summary>
                public object? Key { get; set; }

                /// <summary>
                /// The order of registration. Higher numbers are registered later.
                /// </summary>
                public int Order { get; set; }
            }

            /// <summary>
            /// Shorthand attribute for strategy registration.
            /// Lifetime is not configurable for strategies.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class QudiStrategyAttribute : Attribute
            {
                /// <summary>
                /// Trigger registration only in specific conditions.
                /// </summary>
                public string[]? When { get; set; }

                /// <summary>
                /// The types to register the service as.
                /// It is automatically identified, but you can also specify it explicitly
                /// </summary>
                public Type[]? AsTypes { get; set; }

                /// <summary>
                /// Make this class accessible from other projects?
                /// </summary>
                public bool UsePublic { get; set; }

                /// <summary>
                /// The key for keyed registrations. If null, no key is used.
                /// </summary>
                public object? Key { get; set; }

                /// <summary>
                /// The order of registration. Higher numbers are registered later.
                /// </summary>
                public int Order { get; set; }
            }
        }
        """;
}
