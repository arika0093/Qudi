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

    private const string AttributeMethodUsage =
        "[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]";

    private const string QudiAttributeCode = $$"""
        {{CodeTemplateContents.CommonGeneratedHeader}}
        using System;

        {{CodeTemplateContents.EmbeddedAttributeSource}}

        namespace Qudi
        {
            /// <summary>
            /// Full configuration attribute for Qudi registration without Lifetime. This is used only internally.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{CodeTemplateContents.EditorBrowsableAttribute}}
            {{AttributeClassUsage}}
            public class QudiCoreAttribute : Attribute
            {
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
                /// Specifies how to handle duplicate registrations.
                /// </summary>
                public DuplicateHandling Duplicate { get; set; } = DuplicateHandling.Add;

                /// <summary>
                /// Specifies how AsTypes is inferred when omitted.
                /// </summary>
                public AsTypesFallback AsTypesFallback { get; set; } = AsTypesFallback.SelfOrInterfaces;

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
                /// Whether this registration is a composite.
                /// </summary>
                public bool MarkAsComposite { get; set; }

                /// <summary>
                /// Whether to export this type for visualization. 
                /// When true, generates a separate dependency graph starting from this type.
                /// </summary>
                public bool Export { get; set; }
            }

            /// <summary>
            /// Full configuration attribute for Qudi registration.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public class QudiAttribute : QudiCoreAttribute
            {
                /// <summary>
                /// The lifetime of the registration.
                /// </summary>
                public string? Lifetime { get; set; }
            }

            /// <summary>
            /// Shorthand attribute for singleton lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DISingletonAttribute : QudiCoreAttribute {}

            /// <summary>
            /// Shorthand attribute for transient lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DITransientAttribute : QudiCoreAttribute {}

            /// <summary>
            /// Shorthand attribute for scoped lifetime.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class DIScopedAttribute : QudiCoreAttribute {}

            /// <summary>
            /// Shorthand attribute for decorator registration.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class QudiDecoratorAttribute : QudiCoreAttribute
            {
                /// <summary>
                /// Whether to use interception for this decorator. default is false.
                /// </summary>
                public bool UseIntercept { get; set; }
            }

            /// <summary>
            /// Shorthand attribute for composite registration.
            /// A composite wraps multiple instances of a service type and delegates operations to all of them.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class QudiCompositeAttribute : QudiCoreAttribute
            {
            }

            /// <summary>
            /// Shorthand attribute for dispatch registration.
            /// A dispatch routes calls by runtime argument type for generic interfaces.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeClassUsage}}
            public sealed class QudiDispatchAttribute : QudiCoreAttribute
            {
                /// <summary>
                /// Dispatch target type. If omitted, the generator infers it from the implemented interface generic argument.
                /// </summary>
                public Type? Target { get; set; }

                /// <summary>
                /// Whether dispatch should resolve multiple implementations (default) or a single implementation.
                /// </summary>
                public bool Multiple { get; set; } = true;
            }

            /// <summary>
            /// Specifies how a composite method should handle results from multiple implementations.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{AttributeMethodUsage}}
            public sealed class CompositeMethodAttribute : Attribute
            {
                /// <summary>
                /// The result handling strategy for this composite method.
                /// </summary>
                public CompositeResult Result { get; set; } = CompositeResult.All;

                /// <summary>
                /// The name of a custom result aggregator method.
                /// The method should have signature: TResult MethodName(TResult original, TResult result)
                /// </summary>
                public string? ResultAggregator { get; set; }
            }

            /// <summary>
            /// Defines how a composite method should handle results from multiple implementations.
            /// </summary>
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            public enum CompositeResult
            {
                /// <summary>
                /// Return logical AND of all results.
                /// In Boolean: (a && b && c && ...).
                /// </summary>
                All,

                /// <summary>
                /// Return logical OR of all results.
                /// In Boolean: (a || b || c || ...).
                /// </summary>
                Any,
            }
        }
        """;
}
