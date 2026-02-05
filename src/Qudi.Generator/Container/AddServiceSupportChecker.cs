using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Qudi.Generator.Container;

internal static class AddServiceSupportChecker
{
    public static Dictionary<Type, bool> IsSupported(Compilation compilation)
    {
        var result = new Dictionary<Type, bool>();
        foreach (var generator in AddServiceCodeGenerator.Generators)
        {
            result[generator.GetType()] =
                compilation.GetTypeByMetadataName(generator.SupportCheckMetadataName) is not null;
        }
        return result;
    }
}
