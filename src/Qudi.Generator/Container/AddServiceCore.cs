#pragma warning disable S101 // Types should be named in PascalCase
using Qudi.Generator.Dependency;

namespace Qudi.Generator.Container;

internal abstract class AddServiceCore
{
    protected const string QudiNS = "global::Qudi";
    protected const string QudiGeneratedNS = "global::Qudi.Generated";
    protected const string SystemAction = "global::System.Action";
    protected const string IReadOnlyList = "global::System.Collections.Generic.IReadOnlyList";

    /// <summary>
    /// Metadata name to check whether this generator supports the target dependency.
    /// such as "Qudi.QudiAddServiceForMicrosoftExtensionsDependencyInjection"
    /// </summary>
    public abstract string SupportCheckMetadataName { get; }

    /// <summary>
    /// Return type name of generated AddQudiService method.
    /// such as IServiceCollection (Fully qualified name)
    /// </summary>
    public abstract string ReturnTypeName { get; }

    /// <summary>
    /// Type name which received by generated AddQudiService method.
    /// such as IServiceCollection (Fully qualified name)
    /// </summary>
    public abstract string RecievedTypeName { get; }

    /// <summary>
    /// Called method name inside generated AddQudiService method.
    /// such as AddQudiServiceCore (Fully qualified name)
    /// </summary>
    public abstract string CalledMethodName { get; }

    /// <summary>
    /// Generates the AddQudiService code for the given dependency.
    /// </summary>
    public virtual string? GenerateAddQudiServicesCode(ProjectInfo info)
    {
        return $$"""
            /// <summary>
            /// Registers services in Qudi with optional configuration.
            /// </summary>
            public static {{ReturnTypeName}} AddQudiServices(
                this {{RecievedTypeName}} services,
                {{SystemAction}}<{{QudiNS}}.QudiConfiguration>? configuration = null
            )
            {
                var config = new {{QudiNS}}.QudiConfiguration();
                configuration?.Invoke(config);
                var types = {{QudiGeneratedNS}}.QudiInternalRegistrations.FetchAll(selfOnly: config.UseSelfImplementsOnlyEnabled);
                foreach (var filter in config.Filters)
                {
                    types = types.Where(t => filter(t)).ToList();
                }
                {{CalledMethodName}}(services, types, config);
                return services;
            }
            """;
    }
}
