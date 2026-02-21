#pragma warning disable S101 // Types should be named in PascalCase

using Qudi.Generator.Dependency;

namespace Qudi.Generator.Container;

internal class AddServiceForMicrosoft : AddServiceCore
{
    private const string MEDINamespace = "global::Microsoft.Extensions.DependencyInjection";
    private const string IServiceCollection = $"{MEDINamespace}.IServiceCollection";

    public override string SupportCheckMetadataName =>
        "Qudi.Container.Microsoft.QudiAddServiceToContainer";

    public override string TargetTypeName => IServiceCollection;

    public override string CalledMethodName =>
        $"global::{SupportCheckMetadataName}.AddQudiServices";

    public override string BuilderTypeName => "global::Qudi.QudiMicrosoftConfigurationBuilder";

    protected override string GetBuilderCreationCode(
        string builderVariableName,
        string servicesVariableName
    )
    {
        return $"var {builderVariableName} = new {BuilderTypeName}({servicesVariableName});";
    }

    public override string? GenerateAddQudiServicesCode(ProjectBasicInfo info)
    {
        var isSupported = info.AddServicesAvailable.GetValueOrDefault(GetType(), false);
        if (!isSupported)
        {
            return null;
        }
        return base.GenerateAddQudiServicesCode(info);
    }
}
