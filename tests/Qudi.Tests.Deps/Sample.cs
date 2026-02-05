using Qudi;

namespace Qudi.Tests.Deps;

public interface IDependencyAction
{
    public string SayHello();
}

[DITransient]
internal sealed class NonPublicDependencyAction : IDependencyAction
{
    public string SayHello() => "Hello from DependencyAction";
}
