namespace TrashAnimal;

public class Deck
{
    private List<Card> Cards { get; }

    // Will eventually need a way to load the deck from a saved configuration
    // For now, we can always instantiate the deck brand new
    public Deck()
    {
        /*        
        Points don't need to be on the card class because the rules/engine at the end will perform
        a card count of each player that will handle tie breakers and tally points for the game
        */

        // Todo Iterative approach to generating the deck
        Cards = new List<Card>
        {
            //2 Kitteh (No points)
            new Card(CardName.Kitteh),
            new Card(CardName.Kitteh),
            // 2 Doggo (No points)
            new Card(CardName.Doggo),
            new Card(CardName.Doggo),
            // 5 Yumyum (4, 2, 0)
            new Card(CardName.Yumyum),
            new Card(CardName.Yumyum),
            new Card(CardName.Yumyum),
            new Card(CardName.Yumyum),
            new Card(CardName.Yumyum),
            // 3 Shiny (3, 0 ,0)
            new Card(CardName.Shiny),
            new Card(CardName.Shiny),
            new Card(CardName.Shiny),
            // 11 Nanners (7, 0 ,0)
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            new Card(CardName.Nanners),
            // 9 MmmPie (6, 2, 1)
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            new Card(CardName.MmmPie),
            // 7 Feesh (5, 3, 1)
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            new Card(CardName.Feesh),
            // 13 Blammo (1 point for each stashed)
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
            new Card(CardName.Blammo),
        };
    
        ShuffleDeck();
    }

    public IEnumerable<Card> ShowRemainingCards()
    {
        return Cards;
    }

    public int GetDeckCount()
    {
        return Cards.Count;
    }

    public IEnumerable<Card> DealCards(int n = 1)
    {
        var dealt = Cards.GetRange(0, n);
        Cards.RemoveRange(0, n);

        return dealt;
    }

    /// <summary>
    /// Deals a specific number of cards to each player index in order.
    /// Example for 4 players: counts {3,4,5,6}.
    /// </summary>
    public void DealToPlayers(IReadOnlyList<Player> players, IReadOnlyList<int> startingCounts)
    {
        if (players.Count != startingCounts.Count)
            throw new ArgumentException("Players and counts must have the same length.");

        for (var i = 0; i < players.Count; i++)
        {
            var n = startingCounts[i];
            players[i].AddCards(DealCards(n));
        }
    }

    public void ShuffleDeck(Random? rng = null)
    {
        rng ??= Random.Shared;

        for (int i = Cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (Cards[i], Cards[j]) = (Cards[j], Cards[i]);
        }
    }
}