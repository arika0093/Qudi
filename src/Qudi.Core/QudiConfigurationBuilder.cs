using System;
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Builder for root Qudi configurations.
/// </summary>
public sealed class QudiConfigurationRootBuilder
{
    internal readonly List<QudiConfigurationBuilder> _builders = [];
    internal readonly HashSet<string> _conditions = [];

    // shared builder for common configurations
    internal readonly QudiConfigurationBuilder _sharedBuilder = new()
    {
        // default no-op action
        ConfigurationAction = _ => { },
    };

    /// <summary>
    /// Qudi configuration builders.
    /// </summary>
    public IReadOnlyCollection<QudiConfigurationBuilder> Builders => _builders;

    /// <summary>
    /// Condition keys for conditional registrations.
    /// </summary>
    public IReadOnlyCollection<string> Conditions => _conditions;

    /// <summary>
    /// Adds a new Qudi configuration builder.
    /// </summary>
    public QudiConfigurationBuilder AddService(Action<QudiConfiguration> action)
    {
        var builder = new QudiConfigurationBuilder() { ConfigurationAction = action };
        _builders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds an existing Qudi configuration builder.
    /// </summary>
    public QudiConfigurationRootBuilder AddBuilder(QudiConfigurationBuilder builder)
    {
        _builders.Add(builder);
        return this;
    }

    /// <summary>
    /// Sets a condition key explicitly.
    /// </summary>
    public QudiConfigurationRootBuilder SetCondition(string condition)
    {
        if (!string.IsNullOrWhiteSpace(condition))
        {
            _conditions.Clear();
            _conditions.Add(condition);
        }
        return this;
    }

    /// <summary>
    /// Sets a condition key from the environment variable value.
    /// </summary>
    public QudiConfigurationRootBuilder SetConditionFromEnvironment(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return this;
        }
        var value = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            SetCondition(value);
        }
        return this;
    }

    // --------------------------
    // Below is the shared builder methods wrappers

    /// <summary>
    /// Only register implementations from the current project.
    /// </summary>
    public QudiConfigurationRootBuilder UseSelfImplementsOnly(bool enable = true)
    {
        _sharedBuilder.UseSelfImplementsOnly(enable);
        return this;
    }

    /// <summary>
    /// Adds a filter to modify registrations.
    /// </summary>
    public QudiConfigurationRootBuilder AddFilter(Func<TypeRegistrationInfo, bool> filter)
    {
        _sharedBuilder.AddFilter(filter);
        return this;
    }
}

/// <summary>
/// Builder for Qudi configuration.
/// </summary>
public class QudiConfigurationBuilder
{
    private readonly HashSet<string> _onlyWorkedConditions = [];
    private readonly List<Func<TypeRegistrationInfo, bool>> _filters = [];
    private QudiConfiguration? _configuration;

    /// <summary>
    /// Action to further modify the configuration.
    /// </summary>
    public Action<QudiConfiguration>? ConfigurationAction { get; init; }

    /// <summary>
    /// Conditions on which this builder works.
    /// </summary>
    public IReadOnlyCollection<string> OnlyWorkedConditions => _onlyWorkedConditions;

    /// <summary>
    /// Filters to modify registrations.
    /// </summary>
    public IReadOnlyCollection<Func<TypeRegistrationInfo, bool>> Filters => _filters;

    /// <summary>
    /// Whether only self-implemented registrations are enabled.
    /// </summary>
    public bool? UseSelfImplementsOnlyEnabled { get; private set; } = null;

    /// <summary>
    /// Only register implementations from the current project.
    /// null means not set (use root configuration value).
    /// </summary>
    public QudiConfigurationBuilder UseSelfImplementsOnly(bool? enable = true)
    {
        UseSelfImplementsOnlyEnabled = enable;
        return this;
    }

    /// <summary>
    /// Adds a filter to modify registrations.
    /// </summary>
    public QudiConfigurationBuilder AddFilter(Func<TypeRegistrationInfo, bool> filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// This builder only works on the specified condition.
    /// </summary>
    public QudiConfigurationBuilder OnlyWorkOnSpecificCondition(string condition)
    {
        _onlyWorkedConditions.Clear();
        _onlyWorkedConditions.Add(condition);
        return this;
    }

    /// <summary>
    /// This builder only works on the specified conditions.
    /// </summary>
    public QudiConfigurationBuilder OnlyWorkOnSpecificConditions(IEnumerable<string> conditions)
    {
        _onlyWorkedConditions.Clear();
        foreach (var condition in conditions)
        {
            _onlyWorkedConditions.Add(condition);
        }
        return this;
    }

    /// <summary>
    /// This builder only works on the "Development" condition.
    /// </summary>
    public QudiConfigurationBuilder OnlyWorkOnDevelopment() =>
        OnlyWorkOnSpecificCondition(Condition.Development);

    /// <summary>
    /// This builder only works on the "Production" condition.
    /// </summary>
    public QudiConfigurationBuilder OnlyWorkOnProduction() =>
        OnlyWorkOnSpecificCondition(Condition.Production);

    /// <summary>
    /// Creates a configuration instance for this builder.
    /// </summary>
    protected internal virtual QudiConfiguration CreateConfiguration(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    ) =>
        new QudiConfiguration
        {
            Registrations = registrations,
            Conditions = conditions,
        };

    /// <summary>
    /// Executes the configuration action.
    /// </summary>
    protected internal virtual void Execute()
    {
        if (_configuration is null)
        {
            throw new InvalidOperationException("Configuration has not been initialized.");
        }
        Execute(_configuration);
    }

    /// <summary>
    /// Executes the configuration action with the specified configuration.
    /// </summary>
    protected internal virtual void Execute(QudiConfiguration configuration)
    {
        if (ConfigurationAction is null)
        {
            throw new InvalidOperationException("ConfigurationAction is not set.");
        }
        ConfigurationAction(configuration);
    }

    // internal execution entry point used by the executor
    internal void ExecuteInternal(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    )
    {
        _configuration = CreateConfiguration(registrations, conditions);
        Execute();
    }
}

/// <summary>
/// Builder for Qudi configuration with a specific configuration type.
/// </summary>
public abstract class QudiConfigurationBuilder<TConfiguration> : QudiConfigurationBuilder
    where TConfiguration : QudiConfiguration
{
    private TConfiguration? _typedConfiguration;

    /// <summary>
    /// The strongly-typed configuration instance.
    /// </summary>
    protected TConfiguration Configuration =>
        _typedConfiguration
        ?? throw new InvalidOperationException("Configuration has not been initialized.");

    /// <summary>
    /// Creates a strongly-typed configuration instance for this builder.
    /// </summary>
    protected abstract TConfiguration CreateTypedConfiguration(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    );

    /// <summary>
    /// Executes the configuration action with the strongly-typed configuration.
    /// </summary>
    protected abstract void ExecuteTyped(TConfiguration configuration);

    /// <inheritdoc />
    protected internal sealed override QudiConfiguration CreateConfiguration(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    )
    {
        _typedConfiguration = CreateTypedConfiguration(registrations, conditions);
        return _typedConfiguration;
    }

    /// <inheritdoc />
    protected internal sealed override void Execute() => ExecuteTyped(Configuration);
}

/// <summary>
/// Result of Qudi service configuration.
/// </summary>
public record QudiConfiguration
{
    /// <summary>
    /// Type registrations after applying configuration.
    /// </summary>
    public required IReadOnlyCollection<TypeRegistrationInfo> Registrations { get; init; }

    /// <summary>
    /// Conditions applied during configuration.
    /// </summary>
    public required IReadOnlyCollection<string> Conditions { get; init; }
}
