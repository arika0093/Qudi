using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

// Represents a target for which helper code should be generated.
internal sealed record HelperTarget
{
    public required string InterfaceName { get; init; }
    public required string HelperNamespaceSuffix { get; init; }
    public required EquatableArray<HelperMember> Members { get; init; }
    public required bool IsDecorator { get; init; }
    public required bool IsStrategy { get; init; }
}

// Represents a member (method or property) of a helper interface.
internal sealed record HelperMember
{
    public required HelperMemberKind Kind { get; init; }
    public required string Name { get; init; }
    public required string ReturnTypeName { get; init; }
    public required EquatableArray<HelperParameter> Parameters { get; init; }
    public required bool HasGetter { get; init; }
    public required bool HasSetter { get; init; }
    public required bool IsIndexer { get; init; }
}

// Represents a parameter of a helper member.
internal sealed record HelperParameter
{
    public required string TypeName { get; init; }
    public required string Name { get; init; }
    public required string RefKindPrefix { get; init; }
    public required bool IsParams { get; init; }
}

// The kind of helper member (method or property).
internal enum HelperMemberKind
{
    Method,
    Property,
}
