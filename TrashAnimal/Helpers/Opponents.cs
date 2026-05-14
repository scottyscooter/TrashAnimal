namespace TrashAnimal.Helpers;

class Opponents
{
    /// <summary>
    /// Retrieve all opponents with a non-empty hand.
    /// </summary>
    /// <param name="players">The list of players in the game.</param>
    /// <param name="currentPlayerIndex">The index of the current player.</param>
    /// <returns>An enumerable of opponent indices with a non-empty hand.</returns>        
    public static IEnumerable<int> GetAllWithNonEmptyHand(IReadOnlyList<Player> players, int currentPlayerIndex)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i == currentPlayerIndex)
                continue;
            if (players[i].Hand.Count > 0)
                yield return i;
        }
    }

    /// <summary>
    /// Retrieve all opponents with a non-empty stash.
    /// </summary>
    /// <param name="players">The list of players in the game.</param>
    /// <param name="currentPlayerIndex">The index of the current player.</param>
    /// <returns>An enumerable of opponent indices with a non-empty stash.</returns>       
    public static IEnumerable<int> GetAllWithNonEmptyStash(IReadOnlyList<Player> players, int currentPlayerIndex)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i == currentPlayerIndex)
                continue;
            if (players[i].StashPile.Count > 0)
                yield return i;
        }
    }
}