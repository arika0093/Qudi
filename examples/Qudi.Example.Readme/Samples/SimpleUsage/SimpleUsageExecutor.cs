using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.SimpleUsage;

// ------ Declare services ------
public interface IPokemon
{
    string Name { get; }
    IEnumerable<string> Types { get; }
    void DisplayInfo() =>
        Console.WriteLine($"{Name} is a {string.Join("/", Types)} type Pokémon.");
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

[DISingleton(Export = true)]
public class SimpleUsageExecutor(IServiceProvider serviceProvider) : ISampleExecutor
{
    public string Name => "Simple Usage";
    public string Description => "Basic DI registration with [DISingleton] and [DITransient]";
    public string Namespace => typeof(SimpleUsageExecutor).Namespace!;

    public void Execute()
    {
        var pokemons = serviceProvider.GetServices<IPokemon>();
        foreach (var pokemon in pokemons)
        {
            pokemon.DisplayInfo();
        }
    }
}
