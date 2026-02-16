#!/usr/bin/env dotnet
// #:package Qudi@*
// #:package Qudi.Visualizer@*
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
var displayService = provider.GetRequiredService<DisplayPokemonService>();
displayService.DisplayAll();

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

[DITransient] // ✅️ this is correct!
public class DisplayPokemonService(IEnumerable<IPokemon> pokemons)
{
    public void DisplayAll()
    {
        foreach (var pokemon in pokemons)
        {
            pokemon.DisplayInfo();
        }
    }
}
