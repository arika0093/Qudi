using System.Collections.Generic;

namespace Qudi.Example.Core;

/// <summary>
/// This is an example that only defines the interface, with the implementation provided elsewhere.
/// </summary>
public interface IPokemon
{
    string Name { get; }
    IEnumerable<string> Types { get; }
}

// ----------------------------------------------------------------
// Below are some example implementations of the IPokemon interface.

[DITransient]
internal class Altaria : IPokemon
{
    public string Name => "Altaria";
    public IEnumerable<string> Types => ["Dragon", "Flying"];
}

// Garchomp is only registered in Development environment
[DITransient(When = [Condition.Development])]
internal class Garchomp : IPokemon
{
    public string Name => "Garchomp";
    public IEnumerable<string> Types => ["Dragon", "Ground"];
}

// Lucario is only registered in Production environment
[DITransient(When = [Condition.Production])]
internal class Lucario : IPokemon
{
    public string Name => "Lucario";
    public IEnumerable<string> Types => ["Fighting", "Steel"];
}
