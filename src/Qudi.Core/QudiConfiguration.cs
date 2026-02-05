using System;
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Configuration for Qudi registrations.
/// </summary>
public sealed class QudiConfiguration
{
    private readonly HashSet<string> _conditions = [];

    /// <summary>
    /// Whether only self-implemented registrations are enabled.
    /// </summary>
    public bool UseSelfImplementsOnlyEnabled { get; private set; } = false;

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
}
