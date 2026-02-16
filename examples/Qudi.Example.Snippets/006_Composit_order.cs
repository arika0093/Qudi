#!/usr/bin/env dotnet
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Visualizer;

var services = new ServiceCollection();

// ✅️ register services marked with Qudi attributes
services.AddQudiServices(conf =>
{
    // ✅️ enable visualization output to console and file
    conf.EnableVisualizationOutput(option =>
    {
        option.ConsoleOutput = ConsoleDisplay.All;
        option.AddOutput("summary.md");
    });
});

var provider = services.BuildServiceProvider();
var displayService = provider.GetRequiredService<IPokemon>();
displayService.DisplayInfo();

// ------ Declare services ------
public interface IPokemon
{
    string Name { get; }
    IEnumerable<string> Types { get; }
    public void DisplayInfo() =>
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

[QudiComposite(Order = 0)]
public partial class DisplayPokemonService(IEnumerable<IPokemon> pokemons) : IPokemon { }

[QudiDecorator(Order = 1)]
public partial class PokemonDecorator(IPokemon decorated) : IPokemon
{
    public void DisplayInfo()
    {
        Console.WriteLine("=== Decorated Pokémon Info ===");
        decorated.DisplayInfo();
        Console.WriteLine("==============================");
    }
}
