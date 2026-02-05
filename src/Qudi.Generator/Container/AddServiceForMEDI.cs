#pragma warning disable S101 // Types should be named in PascalCase

namespace Qudi.Generator.Container;

internal class AddServiceForMEDI : AddServiceCore
{
    private const string MEDINamespace = "global::Microsoft.Extensions.DependencyInjection";
    private const string IServiceCollection = $"{MEDINamespace}.IServiceCollection";

    public override string SupportCheckMetadataName =>
        "Qudi.QudiAddServiceForMicrosoftExtensionsDependencyInjection";

    public override string ReturnTypeName => IServiceCollection;

    public override string RecievedTypeName => IServiceCollection;

    public override string CalledMethodName =>
        $"global::{SupportCheckMetadataName}.AddQudiServices";

    public override string? GenerateAddQudiServicesCode(ProjectInfo info)
    {
        var isSupported = info.AddServicesAvailable.GetValueOrDefault(GetType(), false);
        if (!isSupported)
        {
            return null;
        }
        return base.GenerateAddQudiServicesCode(info);
    }
}
