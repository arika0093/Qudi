using System;
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Builder for multiple Qudi configurations.
/// </summary>
public sealed class QudiConfigurationMultiBuilder
{
    internal readonly List<QudiConfigurationBuilder> _builders = [];

    // shared builder for common configurations
    internal readonly QudiConfigurationBuilder _sharedBuilder = new() {
        // default no-op action
        ConfigurationAction = _ => {}
    };

    /// <summary>
    /// Qudi configuration builders.
    /// </summary>
    public IReadOnlyCollection<QudiConfigurationBuilder> Builders => _builders;

    /// <summary>
    /// Adds a new Qudi configuration builder.
    /// </summary>
    public QudiConfigurationBuilder AddService(Action<QudiConfiguration> action)
    {
        var builder = new QudiConfigurationBuilder(){ ConfigurationAction = action };
        _builders.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds an existing Qudi configuration builder.
    /// </summary>
    public QudiConfigurationMultiBuilder AddBuilder(QudiConfigurationBuilder builder)
    {
        _builders.Add(builder);
        return this;
    }

    // --------------------------
    // Below is the shared builder methods wrappers

    /// <summary>
    /// Only register implementations from the current project.
    /// </summary>
    public QudiConfigurationMultiBuilder UseSelfImplementsOnly(bool enable = true)
    {
        _sharedBuilder.UseSelfImplementsOnly(enable);
        return this;
    }

    /// <summary>
    /// Sets a condition key explicitly.
    /// </summary>
    public QudiConfigurationMultiBuilder SetCondition(string condition)
    {
        if (!string.IsNullOrWhiteSpace(condition))
        {
            _sharedBuilder.SetCondition(condition);
        }
        return this;
    }

    /// <summary>
    /// Sets a condition key from the environment variable value.
    /// </summary>
    public QudiConfigurationMultiBuilder SetConditionFromEnvironment(string variableName)
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

    /// <summary>
    /// Adds a filter to modify registrations.
    /// </summary>
    public QudiConfigurationMultiBuilder AddFilter(Func<TypeRegistrationInfo, bool> filter)
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
    private readonly HashSet<string> _conditions = [];
    private readonly List<Func<TypeRegistrationInfo, bool>> _filters = [];

    /// <summary>
    /// Action to further modify the configuration.
    /// </summary>
    public required Action<QudiConfiguration> ConfigurationAction { get; init; }

    /// <summary>
    /// Condition keys for conditional registrations.
    /// </summary>
    public IReadOnlyCollection<string> Conditions => _conditions;

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
    /// Sets a condition key explicitly.
    /// </summary>
    public QudiConfigurationBuilder SetCondition(string condition)
    {
        if (!string.IsNullOrWhiteSpace(condition))
        {
            _conditions.Add(condition);
        }

        return this;
    }

    /// <summary>
    /// Sets a condition key from the environment variable value.
    /// </summary>
    public QudiConfigurationBuilder SetConditionFromEnvironment(string variableName)
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

    /// <summary>
    /// Adds a filter to modify registrations.
    /// </summary>
    public QudiConfigurationBuilder AddFilter(Func<TypeRegistrationInfo, bool> filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// Executes the configuration action.
    /// </summary>
    protected internal virtual void Execute(QudiConfiguration configuration) => ConfigurationAction(configuration);
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