using System.Linq;
using System.Text;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Utility methods shared across helper code generators.
/// </summary>
internal static class HelperCodeGeneratorUtility
{
    private const string Task = "global::System.Threading.Tasks.Task";
    private const string ValueTask = "global::System.Threading.Tasks.ValueTask";

    public static string BuildParameterList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter =>
        {
            var builder = new StringBuilder();
            if (parameter.IsParams)
            {
                builder.Append("params ");
            }

            builder.Append(parameter.RefKindPrefix);
            builder.Append(parameter.TypeName);
            builder.Append(' ');
            builder.Append(parameter.Name);
            return builder.ToString();
        });

        return string.Join(", ", parts);
    }

    public static string BuildArgumentList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter => $"{parameter.RefKindPrefix}{parameter.Name}");

        return string.Join(", ", parts);
    }

    public static string BuildInterceptArgumentList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter =>
            parameter.RefKindPrefix == "out " ? "null" : parameter.Name
        );

        return string.Join(", ", parts);
    }

    public static string BuildInterceptArgumentListWithValue(
        EquatableArray<HelperParameter> parameters
    )
    {
        if (parameters.Count == 0)
        {
            return "value";
        }

        var parts = parameters
            .Select(parameter => parameter.RefKindPrefix == "out " ? "null" : parameter.Name)
            .Concat(["value"]);

        return string.Join(", ", parts);
    }

    public static string BuildHelperInterfaceName(string interfaceHelperName, bool isComposite)
    {
        return isComposite
            ? $"ICompositeHelper_{interfaceHelperName}"
            : $"IDecoratorHelper_{interfaceHelperName}";
    }

    public static bool IsTaskLikeReturnType(string returnType)
    {
        return IsTaskLikeNonGenericReturnType(returnType)
            || returnType.StartsWith($"{Task}<")
            || returnType.StartsWith($"{ValueTask}<");
    }

    public static bool IsTaskLikeNonGenericReturnType(string returnType)
    {
        return returnType == $"{Task}" || returnType == $"{ValueTask}";
    }
}
