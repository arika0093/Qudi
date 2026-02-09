using System;
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Configuration for Qudi registrations.
/// </summary>
public sealed class QudiConfiguration
{
    private readonly HashSet<string> _conditions = [];
    private readonly List<Func<TypeRegistrationInfo, bool>> _filters = [];

    /// <summary>
    /// Whether only self-implemented registrations are enabled.
    /// </summary>
    public bool UseSelfImplementsOnlyEnabled { get; private set; } = false;

    /// <summary>
    /// Filters to modify registrations.
    /// </summary>
    public IReadOnlyCollection<Func<TypeRegistrationInfo, bool>> Filters => _filters;

    /// <summary>
    /// Condition keys for conditional registrations.
    /// </summary>
    public IReadOnlyCollection<string> Conditions => _conditions;

    /// <summary>
    /// Only register implementations from the current project.
    /// </summary>
    public QudiConfiguration UseSelfImplementsOnly()
    {
        UseSelfImplementsOnlyEnabled = true;
        return this;
    }

    /// <summary>
    /// Sets a condition key explicitly.
    /// </summary>
    public QudiConfiguration SetCondition(string condition)
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
    public QudiConfiguration SetConditionFromEnvironment(string variableName)
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
    public QudiConfiguration AddFilter(Func<TypeRegistrationInfo, bool> filter)
    {
        _filters.Add(filter);
        return this;
    }
}
