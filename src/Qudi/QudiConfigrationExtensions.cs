using Microsoft.Extensions.Hosting;

namespace Qudi;

public static class QudiConfigrationExtensions
{
    /// <summary>
    /// Sets a condition key from the host environment.
    /// </summary>
    public static QudiConfiguration SetConditionFromHostEnvironment(
        this QudiConfiguration configuration,
        IHostEnvironment env
    )
    {
        configuration.SetCondition(env.EnvironmentName);
        return configuration;
    }
}
