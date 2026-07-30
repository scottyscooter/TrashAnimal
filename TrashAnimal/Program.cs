using TrashAnimal;
using TrashAnimal.Helpers;
using TrashAnimal.TokenPhase;

var die = new Die();

Console.WriteLine("TrashAnimal (CLI)");
Console.WriteLine();

var playerCount = Cli.ReadIntInRange("How many players (2-4)? ", 2, 4);

var players = new List<Player>(playerCount);
var controllers = new List<IPlayerController>(playerCount);

for (var i = 0; i < playerCount; i++)
{
    var name = Cli.ReadNonEmptyString($"Enter name for player {i + 1}: ");

    var isComputer = false;

    players.Add(new Player(i, name));
    controllers.Add(isComputer ? new AiController(name) : new CliHumanController(name));
    Console.WriteLine();
}

var deck = new Deck();
deck.ShuffleDeck();

var dealCounts = new[] { 3, 4, 5, 6 }.Take(playerCount).ToArray();
deck.DealToPlayers(players, dealCounts);

Console.WriteLine("Initial hands dealt:");
for (var i = 0; i < players.Count; i++)
{
    Console.WriteLine($"- {players[i].Name}: {dealCounts[i]} cards");
}

var session = new GameSession(players, deck);

session.OnFeeshCardSelection = (playerIndex, discardCards) =>
{
    var view = session.GetViewForPlayer(playerIndex);
    return controllers[playerIndex].ChooseFeeshCard(view, discardCards);
};

session.ChooseShinyStealVictim = (thiefIndex, candidates) =>
{
    var view = session.GetViewForPlayer(thiefIndex);
    return controllers[thiefIndex].ChooseShinyStealVictim(view, candidates);
};

Console.WriteLine();
Console.WriteLine("Game start. Press Ctrl+C to quit.");

while (true)
{
    if (session.State == GameState.GameEnded)
    {
        Console.WriteLine();
        var gameEndResult = session.GetGameEndResult();
        foreach (var line in gameEndResult.ScoreLines)
            Console.WriteLine($"{line.PlayerName}: {line.TotalScore}");
        var winningLine = gameEndResult.ScoreLines.Single(line => line.PlayerIndex == gameEndResult.WinningPlayerIndex);
        Console.WriteLine($"Winner: {winningLine.PlayerName}");

        while (true)
        {
            Console.WriteLine();
            Console.Write("Type gg to exit: ");
            var input = Console.ReadLine();
            if (input is not null && input.Trim().Equals("gg", StringComparison.OrdinalIgnoreCase))
                return;
        }
    }

    if (session.State == GameState.TurnEnd)
    {
        Console.WriteLine();
        Console.WriteLine($"-- End of {session.CurrentPlayer.Name}'s turn --");

        var currentController = controllers[session.CurrentPlayerIndex];
        var allowed = session.GetAllowedActionsForPlayer(session.CurrentPlayerIndex);
        var view = session.GetViewForPlayer(session.CurrentPlayerIndex);
        var action = allowed.Contains(GameAction.EndTurn)
            ? currentController.ChooseAction(view, allowed)
            : GameAction.EndTurn;

        if (!session.ApplyAction(session.CurrentPlayerIndex, action, die, out var err, out _) && err is not null)
            Console.WriteLine(err);

        continue;
    }

    if (session.State == GameState.AwaitingStealCardPick)
    {
        var thiefIndex = session.StealThiefIndex
            ?? throw new InvalidOperationException("AwaitingStealCardPick but no thief.");
        var thiefController = controllers[thiefIndex];
        var thiefView = session.GetViewForPlayer(thiefIndex);
        var slots = thiefView.StealPhase?.ThiefPickSlots
            ?? throw new InvalidOperationException("Steal pick slots missing from view.");
        var cardId = thiefController.ChooseStealCard(thiefView, slots);
        if (!session.TryCompleteStealWithCard(thiefIndex, cardId, out var stealErr, out _) && stealErr is not null)
            Console.WriteLine(stealErr);

        continue;
    }

    if (session.State == GameState.AwaitingStealResponse)
    {
        var victimIndex = session.StealVictimIndex
            ?? throw new InvalidOperationException("AwaitingStealResponse but no victim.");
        var victimController = controllers[victimIndex];
        var victimView = session.GetViewForPlayer(victimIndex);
        var allowed = session.GetAllowedActionsForPlayer(victimIndex);
        var stealAction = victimController.ChooseAction(victimView, allowed);
        if (!session.ApplyAction(victimIndex, stealAction, die, out var stealRespondErr, out _) && stealRespondErr is not null)
            Console.WriteLine(stealRespondErr);

        continue;
    }

    if (session.State == GameState.AwaitingYumYum)
    {
        var responderIndex = session.GetCurrentYumYumResponderIndex();
        if (responderIndex is null)
            throw new InvalidOperationException("AwaitingYumYum but no responder.");

        var responderController = controllers[responderIndex.Value];
        var responderView = session.GetViewForPlayer(responderIndex.Value);
        var responderAllowed = session.GetAllowedActionsForPlayer(responderIndex.Value);
        var action = responderAllowed.Contains(GameAction.YumYumPlay) && responderController.ChoosePlayYumYum(responderView)
            ? GameAction.YumYumPlay
            : GameAction.YumYumPass;

        if (!session.ApplyAction(responderIndex.Value, action, die, out var err, out _) && err is not null)
            Console.WriteLine(err);

        continue;
    }

    if (session.State == GameState.TokenPhase)
    {
        var currentPlayerIndex = session.CurrentPlayerIndex;
        var active = players[currentPlayerIndex];
        var controller = controllers[currentPlayerIndex];
        var view = session.GetViewForPlayer(currentPlayerIndex);
        var tp = view.TokenPhase;

        if (tp?.Step == TokenPhaseStep.RecycleChoosingReplacement)
        {
            var opts = session.GetTokenPhaseRecycleOptions();
            var pick = controller.ChooseRecycleReplacement(view, opts);
            if (!session.TryTokenPhaseRecyclePick(currentPlayerIndex, pick, out var recErr, out _) && recErr is not null)
                Console.WriteLine(recErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            var responderIdx = tp.BanditCurrentResponderIndex
                ?? throw new InvalidOperationException("Bandit responder missing.");
            var responderController = controllers[responderIdx];
            var responderView = session.GetViewForPlayer(responderIdx);
            responderController.ChooseBanditResponse(responderView, out var stash, out var cardId);
            if (stash && cardId is { } gid)
            {
                if (!session.TryBanditStashMatchingCard(responderIdx, gid, out var bErr, out _) && bErr is not null)
                    Console.WriteLine(bErr);
            }
            else if (!session.TryBanditPass(responderIdx, out var pErr, out _) && pErr is not null)
                Console.WriteLine(pErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.DoubleStashChoosingCards)
        {
            var ids = controller.ChooseDoubleStashCardIds(view, tp.StashableHandCardsForCurrentPrompt);
            if (!session.TryTokenPhaseDoubleStash(currentPlayerIndex, ids, out var dsErr, out _) && dsErr is not null)
                Console.WriteLine(dsErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.StashTrashPickCard)
        {
            var cardId = controller.ChooseStashTrashStashCard(view, tp.StashableHandCardsForCurrentPrompt);
            if (!session.TryTokenPhaseStashTrashPickCard(currentPlayerIndex, cardId, out var stErr, out _) && stErr is not null)
                Console.WriteLine(stErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.StealChoosingVictim)
        {
            PromptAndStartTokenSteal(session, players, controllers, currentPlayerIndex, view);
            continue;
        }

        var allowedActions = session.GetAllowedActionsForPlayer(currentPlayerIndex);

        Console.WriteLine();
        Console.WriteLine($"-- {active.Name}'s TokenPhase --");
        if (tp is not null)
        {
            Cli.PrintTokens(tp.RemainingTokens);
            Console.WriteLine($"Step: {tp.Step}");
            if (tp.BanditRevealedCardName is { } br)
                Console.WriteLine($"Bandit revealed (public): {br}");
        }

        var playerAction = controller.ChooseAction(view, allowedActions);

        if (playerAction == GameAction.TokenStashTrashStashMode)
        {
            if (!session.ApplyAction(currentPlayerIndex, playerAction, die, out var e1, out _) && e1 is not null)
                Console.WriteLine(e1);
            continue;
        }

        if (playerAction == GameAction.TokenDoubleStashSubmit)
        {
            var ids = controller.ChooseDoubleStashCardIds(view, tp?.StashableHandCardsForCurrentPrompt ?? Array.Empty<StashableHandCard>());
            if (!session.TryTokenPhaseDoubleStash(currentPlayerIndex, ids, out var e2, out _) && e2 is not null)
                Console.WriteLine(e2);
            continue;
        }

        if (playerAction == GameAction.ResolveTokenSteal)
        {
            // Steal always needs a victim, so it is never dispatched as a plain action — it goes through
            // the same explicit-choice entry point the API/frontend use, whether this is the first pick
            // (ChoosingNextToken) or an MmmPie repeat (StealChoosingVictim, handled above).
            PromptAndStartTokenSteal(session, players, controllers, currentPlayerIndex, view);
            continue;
        }

        if (!session.ApplyAction(currentPlayerIndex, playerAction, die, out var error, out _) && error is not null)
            Console.WriteLine(error);

        continue;
    }

    var rollPlayerIndex = session.CurrentPlayerIndex;
    var rollActive = players[rollPlayerIndex];
    var rollController = controllers[rollPlayerIndex];
    var rollAllowed = session.GetAllowedActionsForPlayer(rollPlayerIndex);
    var rollView = session.GetViewForPlayer(rollPlayerIndex);

    Console.WriteLine();
    Console.WriteLine($"-- {rollActive.Name}'s RollPhase --");

    var rollAction = rollController.ChooseAction(rollView, rollAllowed);
    if (!session.ApplyAction(rollPlayerIndex, rollAction, die, out var rollError, out _) && rollError is not null)
        Console.WriteLine(rollError);
}

// Drives the Steal token's explicit-choice API for both the first pick (GameAction.ResolveTokenSteal from
// ChoosingNextToken) and an MmmPie repeat (TokenPhaseStep.StealChoosingVictim) — the CLI now uses the same
// entry point as the API/frontend instead of a Func<> delegate. If no opponent has any card in hand, it
// passes victimIndex: null so the session auto-resolves the fizzle instead of prompting.
void PromptAndStartTokenSteal(
    GameSession activeSession,
    List<Player> allPlayers,
    List<IPlayerController> allControllers,
    int thiefIndex,
    GameView thiefView)
{
    var candidates = Opponents.GetAllWithNonEmptyHand(allPlayers, thiefIndex).ToList();
    int? victimIndex = candidates.Count == 0
        ? null
        : allControllers[thiefIndex].ChooseTokenStealVictim(thiefView, candidates);

    if (!activeSession.TryStartTokenStealWithVictimChoice(thiefIndex, victimIndex, out var stealError, out _)
        && stealError is not null)
        Console.WriteLine(stealError);
}
