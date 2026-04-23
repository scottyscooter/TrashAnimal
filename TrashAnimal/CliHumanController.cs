namespace TrashAnimal;

public sealed class CliHumanController : IPlayerController
{
    public CliHumanController(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }

    public GameAction ChooseAction(GameView view, IReadOnlyList<GameAction> allowedActions)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"== {view.CurrentPlayerName} ==");
            Console.WriteLine($"State: {view.State}  Busted={view.IsBusted}  ForcedRoll={view.ForcedRollRemaining}");
            Cli.PrintTokens(view.PhaseOneTokens);
            Console.WriteLine($"Hand: {(view.HandCardNames.Count == 0 ? "(empty)" : string.Join(", ", view.HandCardNames))}");
            Console.WriteLine();

            for (var i = 0; i < allowedActions.Count; i++)
                Console.WriteLine($"{i + 1}. {allowedActions[i]}");

            var choice = Cli.ReadIntInRange("Choose action: ", 1, allowedActions.Count);
            return allowedActions[choice - 1];
        }
    }

    public bool ChoosePlayYumYum(GameView view)
    {
        // Only asked for the current responder.
        return Cli.ReadYesNo($"{DisplayName}: play YumYum to force {view.CurrentPlayerName} to roll once? (y/n) ");
    }

    public Card? ChooseFeeshCard(GameView view, IReadOnlyList<Card> discardPile)
    {
        if (discardPile.Count == 0)
            return null;

        Console.WriteLine("Discard pile:");
        for (var i = 0; i < discardPile.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {discardPile[i].Name}");
        }

        var choice = Cli.ReadIntInRange("Choose a card to recover: ", 1, discardPile.Count);
        return discardPile[choice - 1];
    }
}

