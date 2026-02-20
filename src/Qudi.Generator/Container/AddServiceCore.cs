#pragma warning disable S101 // Types should be named in PascalCase
using Qudi.Generator.Dependency;

namespace Qudi.Generator.Container;

internal abstract class AddServiceCore
{
    protected const string QudiNS = "global::Qudi";
    protected const string QudiGeneratedNS = "global::Qudi.Generated";
    protected const string QudiExecuteAllMethod =
        $"{QudiNS}.Internal.QudiConfigurationExecutor.ExecuteAll";
    protected const string SystemAction = "global::System.Action";
    protected const string IReadOnlyList = "global::System.Collections.Generic.IReadOnlyList";

    /// <summary>
    /// Metadata name to check whether this generator supports the target dependency.
    /// such as "Qudi.QudiAddServiceForMicrosoftExtensionsDependencyInjection"
    /// </summary>
    public abstract string SupportCheckMetadataName { get; }

    /// <summary>
    /// Target type name to extend.
    /// such as "IServiceCollection" (Fully qualified name)
    /// </summary>
    public abstract string TargetTypeName { get; }

    /// <summary>
    /// Called method name inside generated AddQudiService method.
    /// such as AddQudiServiceCore (Fully qualified name)
    /// </summary>
    public abstract string CalledMethodName { get; }

    /// <summary>
    /// Generates the AddQudiService code for the given dependency.
    /// </summary>
    public virtual string? GenerateAddQudiServicesCode(ProjectBasicInfo info)
    {
        return $$"""
            /// <summary>
            /// Registers services in Qudi with optional configuration.
            /// </summary>
            public static {{TargetTypeName}} AddQudiServices(
                this {{TargetTypeName}} services
            )
            {
                return AddQudiServices(services, (_mb, _b) => { });
            }

            /// <summary>
            /// Registers services in Qudi with optional configuration.
            /// </summary>
            /// <param name="configuration">Configuration action for builder.</param>
            public static {{TargetTypeName}} AddQudiServices(
                this {{TargetTypeName}} services,
                {{SystemAction}}<{{QudiNS}}.QudiConfigurationRootBuilder>? configuration
            )
            {
                return AddQudiServices(services, (multiBuilder, _) => configuration?.Invoke(multiBuilder));
            }

            /// <summary>
            /// Registers services in Qudi with optional configuration.
            /// </summary>
            /// <param name="configuration">
            /// Configuration action for builder. The first parameter is the multi-builder. the second is the builder for this dependency.
            /// </param>
            public static {{TargetTypeName}} AddQudiServices(
                this {{TargetTypeName}} services,
                {{SystemAction}}<{{QudiNS}}.QudiConfigurationRootBuilder, {{QudiNS}}.QudiConfigurationBuilder>? configuration
            )
            {
                var multiBuilder = new {{QudiNS}}.QudiConfigurationRootBuilder();
            #if DEBUG
                multiBuilder.SetCondition({{QudiNS}}.Condition.Development);
            #else
                multiBuilder.SetCondition({{QudiNS}}.Condition.Production);
            #endif
                var builderOfCurrent = new {{QudiNS}}.QudiConfigurationBuilder()
                {
                    ConfigurationAction = (config) => {{CalledMethodName}}(services, config)
                };
                configuration?.Invoke(multiBuilder, builderOfCurrent);
                multiBuilder.AddBuilder(builderOfCurrent);
                {{QudiExecuteAllMethod}}(multiBuilder, {{QudiGeneratedNS}}.QudiInternalRegistrations.FetchAll);
                return services;
            }
            """;
    }
}
