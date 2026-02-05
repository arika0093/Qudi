using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Qudi.Generator.Utility;

internal static class ValueProviderUtility
{
    /// <summary>
    /// Combines two IncrementalValueProviders of ImmutableArray<T> and merges their contents.
    /// </summary>
    public static IncrementalValueProvider<ImmutableArray<T>> CombineAndMerge<T>(
        this IncrementalValueProvider<ImmutableArray<T>> root,
        IncrementalValueProvider<ImmutableArray<T>> other
    )
    {
        return root.Combine(other)
            .Select(
                static (combined, _) =>
                {
                    var (first, second) = combined;
                    return first.AddRange(second);
                }
            );
    }
}
