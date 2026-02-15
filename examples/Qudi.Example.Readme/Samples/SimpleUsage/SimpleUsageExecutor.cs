using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.SimpleUsage;

// ------ Declare services ------
public interface IPokemon
{
    string Name { get; }
    IEnumerable<string> Types { get; }
    void DisplayInfo() => Console.WriteLine($"{Name} is a {string.Join("/", Types)} type Pokémon.");
}

[DISingleton] // ✅️ mark as singleton
public class Altaria : IPokemon
{
    public string Name => "Altaria";
    public IEnumerable<string> Types => ["Dragon", "Flying"];
}

[DITransient] // ✅️ mark as transient, too
public class Abomasnow : IPokemon
{
    public string Name => "Abomasnow";
    public IEnumerable<string> Types => ["Grass", "Ice"];
}

[DIScoped]
public class Azumarill : IPokemon
{
    public string Name => "Azumarill";
    public IEnumerable<string> Types => ["Water", "Fairy"];
}

[DITransient(Export = true)]
public class SimpleUsageExecutor(IEnumerable<IPokemon> pokemons) : ISampleExecutor
{
    public string Name => "Simple Usage";
    public string Description =>
        "Basic DI registration with [DISingleton], [DIScoped] and [DITransient]";
    public string Namespace => typeof(SimpleUsageExecutor).Namespace!;

    public void Execute()
    {
        foreach (var pokemon in pokemons)
        {
            pokemon.DisplayInfo();
        }
    }
}
