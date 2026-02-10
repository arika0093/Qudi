using System.Collections.Generic;

namespace Qudi.Example.Core;

/// <summary>
/// This is a example service that uses dependency injection to get all registered IPokemon implementations
/// </summary>
[DISingleton]
public class NotifyPokemonInfoService(
    IEnumerable<IPokemon> pokemons,
    INotificationService notificationService
)
{
    public void NotifyAll()
    {
        foreach (var pokemon in pokemons)
        {
            var info = $"Pokemon: {pokemon.Name}, Types: {string.Join(", ", pokemon.Types)}";
            notificationService.Notify(info);
        }
    }
}
