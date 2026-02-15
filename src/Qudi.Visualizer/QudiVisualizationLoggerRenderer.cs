using System;
using Microsoft.Extensions.Logging;

namespace Qudi.Visualizer;

internal static class QudiVisualizationLoggerRenderer
{
    public static void Render(ILogger logger, QudiVisualizationReport report)
    {
        logger.LogInformation(
            "Qudi Visualization Summary: Registrations={RegistrationCount}, Missing={MissingCount}, Cycles={CycleCount}, Multiple={MultipleRegistrationCount}, LifetimeWarnings={LifetimeWarningCount}",
            report.Summary.RegistrationCount,
            report.Summary.MissingCount,
            report.Summary.CycleCount,
            report.Summary.MultipleRegistrationCount,
            report.Summary.LifetimeWarningCount
        );
        logger.LogDebug(
            "Qudi Visualization Registrations ({RegistrationCount})",
            report.Registrations.Count
        );

        foreach (var row in report.Registrations)
        {
            logger.LogDebug(
                "Service={Service}, Implementation={Implementation}, Lifetime={Lifetime}, Key={Key}, When={When}, Order={Order}, Decorator={Decorator}",
                row.Service,
                row.Implementation,
                row.Lifetime,
                row.Key,
                row.When,
                row.Order,
                row.Decorator
            );
        }

        foreach (var missing in report.MissingRegistrations)
        {
            logger.LogWarning(
                "Missing registration: Required={RequiredType}, RequestedBy={RequestedBy}",
                missing.RequiredType,
                missing.RequestedBy
            );
        }

        foreach (var cycle in report.Cycles)
        {
            logger.LogWarning("Cycle detected: {CyclePath}", string.Join(" -> ", cycle.Path));
        }

        foreach (var warning in report.LifetimeWarnings)
        {
            logger.LogWarning(
                "Lifetime warning: Service={Service}, From={From}, To={To}, Message={Message}",
                warning.Service,
                warning.From,
                warning.To,
                warning.Message
            );
        }
    }
}
