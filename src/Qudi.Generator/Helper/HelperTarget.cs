using Microsoft.CodeAnalysis;

namespace Qudi.Generator.Helper;

internal sealed record HelperTarget(
    INamedTypeSymbol InterfaceSymbol,
    bool IsDecorator,
    bool IsStrategy
);
