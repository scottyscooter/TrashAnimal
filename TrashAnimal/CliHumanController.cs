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
            if (view.StealPhase is { } sp)
                Console.WriteLine($"Steal: {sp.StealingPlayerName} -> {sp.VictimName}'s {sp.InitialStealTargetZone}");
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

    public int ChooseShinyStealVictim(GameView view, IReadOnlyList<int> opponentIndicesWithNonEmptyStash)
    {
        if (opponentIndicesWithNonEmptyStash.Count == 0)
            throw new InvalidOperationException("Shiny victim choice requires at least one candidate.");

        Console.WriteLine();
        Console.WriteLine($"{DisplayName}: choose whose stash to steal from:");
        for (var i = 0; i < opponentIndicesWithNonEmptyStash.Count; i++)
            Console.WriteLine($"{i + 1}. Player index {opponentIndicesWithNonEmptyStash[i]}");

        var choice = Cli.ReadIntInRange("Choice: ", 1, opponentIndicesWithNonEmptyStash.Count);
        return opponentIndicesWithNonEmptyStash[choice - 1];
    }

    public Guid ChooseStealCard(GameView view, IReadOnlyList<StealPickSlot> slots)
    {
        if (slots.Count == 0)
            throw new InvalidOperationException("Steal card choice requires at least one slot.");

        Console.WriteLine();
        Console.WriteLine($"{DisplayName}: choose a card to steal:");
        for (var i = 0; i < slots.Count; i++)
            Console.WriteLine($"{i + 1}. {slots[i].ThiefFacingLabel} (slot {i + 1})");

        var choice = Cli.ReadIntInRange("Choice: ", 1, slots.Count);
        return slots[choice - 1].CardId;
    }
}

