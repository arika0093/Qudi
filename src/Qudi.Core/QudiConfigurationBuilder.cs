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

    /// <summary>
    /// Action to further modify the configuration.
    /// </summary>
    public required Action<QudiConfiguration> ConfigurationAction { get; init; }

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
    /// Executes the configuration action.
    /// </summary>
    protected internal virtual void Execute(QudiConfiguration configuration) =>
        ConfigurationAction(configuration);
}

/// <summary>
/// Result of Qudi service configuration.
/// </summary>
public sealed record QudiConfiguration
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
