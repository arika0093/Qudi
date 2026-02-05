
## Coding Rules
### MUST
- Follow the [Source Generator Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md):
  - Use `ForAttributeWithMetadataName`
  - Use `IndentStringBuilder` for code generation.
  - Apply `Microsoft.CodeAnalysis.EmbeddedAttribute` to generated marker types.
- All generated code must reference types using fully qualified names with `global::`.
- Syntax provider results must remain idempotent:
  - Always use `record` types.
  - Do not include `ISymbol` information (results would vary on each run).
  - Use `EquatableArray<T>` instead of arrays.
- When using record types, use the form `record Sample { public string A {get;init;} }` instead of `record Sample(string A)`.
- Use `[]` instead of `new T[]`/`new List<T>` for array initializers in generated code.

### DO NOT
- Use reflection at runtime to discover types or attributes.
- Use assembly scanning.