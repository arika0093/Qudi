#pragma warning disable CS9113
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Dummy base class for decorator helpers.
/// </summary>
public abstract class DecoratorHelper<T>(T inner)
{
    /// <summary>
    /// Intercepts method calls to the inner service.
    /// </summary>
    protected abstract IEnumerable<T> Intercept(string methodName, object?[] parameters);
}
