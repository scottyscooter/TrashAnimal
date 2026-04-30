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
        string Label(GameAction action) =>
            action == GameAction.AbandonBust
                ? "Busted: Draw one card and end turn."
                : action.ToString();

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
                Console.WriteLine($"{i + 1}. {Label(allowedActions[i])}");

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

    public void ChooseBanditResponse(GameView view, out bool stash, out Guid? cardId)
    {
        stash = false;
        cardId = null;
        var tp = view.TokenPhase ?? throw new InvalidOperationException("TokenPhase view missing.");
        var stashable = tp.StashableHandCardsForCurrentPrompt;
        if (stashable.Count == 0)
        {
            Console.WriteLine($"{DisplayName}: no matching card to stash — pass.");
            return;
        }

        var pass = !Cli.ReadYesNo($"{DisplayName}: stash a matching {tp.BanditRevealedCardName}? (y/n) ");
        if (pass)
            return;

        Console.WriteLine("Pick a card to stash:");
        for (var i = 0; i < stashable.Count; i++)
            Console.WriteLine($"{i + 1}. {stashable[i].Name}");

        var choice = Cli.ReadIntInRange("Choice: ", 1, stashable.Count);
        stash = true;
        cardId = stashable[choice - 1].CardId;
    }

    public IReadOnlyList<Guid> ChooseDoubleStashCardIds(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable)
    {
        if (stashable.Count == 0)
        {
            Console.WriteLine($"{DisplayName}: no stashable cards — submitting 0.");
            return Array.Empty<Guid>();
        }

        Console.WriteLine($"{DisplayName}: DoubleStash — pick 0–2 stashable cards (enter blank line to finish).");
        var chosen = new List<Guid>();
        for (var n = 0; n < 2; n++)
        {
            Console.WriteLine("Stashable:");
            for (var i = 0; i < stashable.Count; i++)
            {
                var already = chosen.Contains(stashable[i].Id) ? " (already picked)" : "";
                Console.WriteLine($"{i + 1}. {stashable[i].Name}{already}");
            }

            Console.Write($"Card {n + 1} (or Enter to stop): ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                break;

            if (!int.TryParse(line.Trim(), out var idx) || idx < 1 || idx > stashable.Count)
            {
                Console.WriteLine("Invalid — stopping picks.");
                break;
            }

            var id = stashable[idx - 1].Id;
            if (!chosen.Contains(id))
                chosen.Add(id);
        }

        return chosen;
    }

    public Guid ChooseStashTrashStashCard(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable)
    {
        if (stashable.Count == 0)
            throw new InvalidOperationException("No stashable cards.");

        Console.WriteLine($"{DisplayName}: choose a card to stash face down:");
        for (var i = 0; i < stashable.Count; i++)
            Console.WriteLine($"{i + 1}. {stashable[i].Name}");

        var choice = Cli.ReadIntInRange("Choice: ", 1, stashable.Count);
        return stashable[choice - 1].Id;
    }

    public TokenAction ChooseRecycleReplacement(GameView view, IReadOnlyList<TokenAction> options)
    {
        if (options.Count == 0)
            throw new InvalidOperationException("No recycle options.");

        Console.WriteLine($"{DisplayName}: choose replacement token:");
        for (var i = 0; i < options.Count; i++)
            Console.WriteLine($"{i + 1}. {options[i]}");

        var choice = Cli.ReadIntInRange("Choice: ", 1, options.Count);
        return options[choice - 1];
    }
}

