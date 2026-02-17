using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Represents a target for which helper code should be generated.
/// </summary>
internal sealed record HelperInterfaceTarget
{
    /// <summary>
    /// Fully qualified interface name (e.g., global::Foo.Bar.IFoo).
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// Namespace that contains the interface.
    /// </summary>
    public required string InterfaceNamespace { get; init; }

    /// <summary>
    /// Sanitized name used to build helper identifiers.
    /// </summary>
    public required string InterfaceHelperName { get; init; }

    /// <summary>
    /// Suffix used for helper namespace generation.
    /// </summary>
    public required string HelperNamespaceSuffix { get; init; }

    /// <summary>
    /// Constructor parameter name used to access the inner service.
    /// </summary>
    public required string DecoratorParameterName { get; init; }

    /// <summary>
    /// Members collected from the interface (and its inherited interfaces).
    /// </summary>
    public required EquatableArray<HelperMember> Members { get; init; }

    /// <summary>
    /// Whether the target type is a decorator.
    /// </summary>
    public required bool IsDecorator { get; init; }

    /// <summary>
    /// Whether the target type is a composite.
    /// </summary>
    public required bool IsComposite { get; init; }

    /// <summary>
    /// Whether intercept-style helpers should be generated.
    /// </summary>
    public required bool UseIntercept { get; init; }

    /// <summary>
    /// Generic type parameters with constraints (e.g., "T where T : IComponent").
    /// </summary>
    public required string GenericTypeParameters { get; init; }

    /// <summary>
    /// Generic type arguments for use in constructed types (e.g., "<T>").
    /// </summary>
    public required string GenericTypeArguments { get; init; }
}

/// <summary>
/// Represents a containing (parent) type for nested classes.
/// </summary>
internal sealed record ContainingTypeInfo
{
    /// <summary>
    /// Name of the containing type.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type keyword (e.g., class, struct, record).
    /// </summary>
    public required string TypeKeyword { get; init; }

    /// <summary>
    /// Accessibility of the containing type.
    /// </summary>
    public required string Accessibility { get; init; }
}

/// <summary>
/// Represents a target type that should implement a decorator helper interface.
/// </summary>
internal sealed record HelperImplementingTarget
{
    /// <summary>
    /// Name of the implementing type (without namespace).
    /// </summary>
    public required string ImplementingTypeName { get; init; }

    /// <summary>
    /// Namespace of the implementing type.
    /// </summary>
    public required string ImplementingTypeNamespace { get; init; }

    // such as class, struct, record,
    /// <summary>
    /// Keyword describing the implementing type (e.g., class, struct, record).
    /// </summary>
    public required string ImplementingTypeKeyword { get; init; }

    /// <summary>
    /// Parent classes if this is a nested class (ordered from outermost to innermost).
    /// </summary>
    public required EquatableArray<ContainingTypeInfo> ContainingTypes { get; init; }

    /// <summary>
    /// Accessibility of the constructor used for helper generation.
    /// </summary>
    public required string ConstructorAccessibility { get; init; }

    /// <summary>
    /// Fully qualified interface name this helper is generated for.
    /// </summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// Namespace that contains the interface.
    /// </summary>
    public required string InterfaceNamespace { get; init; }

    /// <summary>
    /// Sanitized helper name for the interface.
    /// </summary>
    public required string InterfaceHelperName { get; init; }

    /// <summary>
    /// Constructor parameters used when building the partial helper.
    /// </summary>
    public required EquatableArray<HelperParameter> ConstructorParameters { get; init; }

    /// <summary>
    /// Name of the parameter used as the inner service in decorators.
    /// </summary>
    public required string BaseParameterName { get; init; }

    /// <summary>
    /// Whether the target type is a decorator.
    /// </summary>
    public required bool IsDecorator { get; init; }

    /// <summary>
    /// Whether the target type is a composite.
    /// </summary>
    public required bool IsComposite { get; init; }

    /// <summary>
    /// Whether intercept-style helpers should be generated.
    /// </summary>
    public required bool UseIntercept { get; init; }

    /// <summary>
    /// Generic type parameters with constraints (e.g., "T where T : IComponent").
    /// </summary>
    public required string GenericTypeParameters { get; init; }

    /// <summary>
    /// Generic type arguments for use in constructed types (e.g., "<T>").
    /// </summary>
    public required string GenericTypeArguments { get; init; }

    /// <summary>
    /// Composite methods declared in the target class that should be auto-implemented.
    /// </summary>
    public required EquatableArray<CompositeMethodOverride> CompositeMethodOverrides { get; init; }
}

/// <summary>
/// Represents input data for helper code generation.
/// </summary>
internal sealed record HelperGenerationInput
{
    /// <summary>
    /// Helper interface targets to generate.
    /// </summary>
    public required EquatableArray<HelperInterfaceTarget> InterfaceTargets { get; init; }

    /// <summary>
    /// Types that should implement helper interfaces.
    /// </summary>
    public required EquatableArray<HelperImplementingTarget> ImplementingTargets { get; init; }

    /// <summary>
    /// Dispatch-based composite targets.
    /// </summary>
    public required EquatableArray<DispatchCompositeTarget> DispatchCompositeTargets { get; init; }
}

/// <summary>
/// Represents a dispatch-based composite target.
/// </summary>
internal sealed record DispatchCompositeTarget
{
    public required string ImplementingTypeName { get; init; }
    public required string ImplementingTypeNamespace { get; init; }
    public required string ImplementingTypeKeyword { get; init; }
    public required string ImplementingTypeAccessibility { get; init; }
    public required EquatableArray<ContainingTypeInfo> ContainingTypes { get; init; }
    public required string InterfaceName { get; init; }
    public required string InterfaceHelperName { get; init; }
    public required EquatableArray<DispatchCompositeMethod> Methods { get; init; }
    public required string GenericTypeParameters { get; init; }
    public required string GenericTypeArguments { get; init; }
    public required EquatableArray<DispatchCompositeConcreteType> ConcreteTypes { get; init; }
    public required EquatableArray<DispatchCompositeConstraintType> ConstraintTypes { get; init; }
    public required EquatableArray<CompositeMethodOverride> CompositeMethodOverrides { get; init; }
}

/// <summary>
/// Method metadata for dispatch composites.
/// </summary>
internal sealed record DispatchCompositeMethod
{
    public required HelperMember Member { get; init; }
    public required int DispatchParameterIndex { get; init; }
}

/// <summary>
/// Concrete type metadata for dispatch composites.
/// </summary>
internal sealed record DispatchCompositeConcreteType
{
    public required string TypeName { get; init; }
    public required string FieldName { get; init; }
    public required string ParameterName { get; init; }
    public required string ConstructedInterfaceTypeName { get; init; }
}

/// <summary>
/// Constraint type metadata for dispatch composites.
/// </summary>
internal sealed record DispatchCompositeConstraintType
{
    public required string TypeName { get; init; }
    public required string Suffix { get; init; }
    public required string ConstructedInterfaceTypeName { get; init; }
}

/// <summary>
/// Represents a member (method or property) of a helper interface.
/// </summary>
internal sealed record HelperMember
{
    /// <summary>
    /// Kind of the member (method or property).
    /// </summary>
    public required HelperMemberKind Kind { get; init; }

    /// <summary>
    /// Member name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Fully qualified return type name.
    /// </summary>
    public required string ReturnTypeName { get; init; }

    /// <summary>
    /// Declaring interface for explicit implementation.
    /// </summary>
    public required string DeclaringInterfaceName { get; init; }

    /// <summary>
    /// Parameters of the member.
    /// </summary>
    public required EquatableArray<HelperParameter> Parameters { get; init; }

    /// <summary>
    /// Whether the property has a getter.
    /// </summary>
    public required bool HasGetter { get; init; }

    /// <summary>
    /// Whether the property has a setter.
    /// </summary>
    public required bool HasSetter { get; init; }

    /// <summary>
    /// Whether the property is an indexer.
    /// </summary>
    public required bool IsIndexer { get; init; }
}

/// <summary>
/// Represents a parameter of a helper member.
/// </summary>
internal sealed record HelperParameter
{
    /// <summary>
    /// Fully qualified parameter type name.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Prefix for ref/out/in parameters.
    /// </summary>
    public required string RefKindPrefix { get; init; }

    /// <summary>
    /// Whether the parameter is a params array.
    /// </summary>
    public required bool IsParams { get; init; }
}

/// <summary>
/// Represents an explicitly declared composite method in a target class.
/// </summary>
internal sealed record CompositeMethodOverride
{
    /// <summary>
    /// Method name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Fully qualified return type name.
    /// </summary>
    public required string ReturnTypeName { get; init; }

    /// <summary>
    /// Method parameters.
    /// </summary>
    public required EquatableArray<HelperParameter> Parameters { get; init; }

    /// <summary>
    /// Declared result behavior for composite execution.
    /// </summary>
    public required CompositeResultBehavior ResultBehavior { get; init; }
}

/// <summary>
/// Composite aggregation behavior.
/// </summary>
internal enum CompositeResultBehavior
{
    All,
    Any,
    Sequential,
}

/// <summary>
/// The kind of helper member (method or property).
/// </summary>
internal enum HelperMemberKind
{
    /// <summary>
    /// A method member.
    /// </summary>
    Method,

    /// <summary>
    /// A property member.
    /// </summary>
    Property,
}
