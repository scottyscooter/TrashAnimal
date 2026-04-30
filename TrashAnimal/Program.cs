using TrashAnimal;
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

session.ChooseTokenHandStealVictim = (thiefIndex, candidates) =>
{
    var view = session.GetViewForPlayer(thiefIndex);
    Console.WriteLine();
    Console.WriteLine($"{players[thiefIndex].Name}: choose a victim to steal one card from hand:");
    for (var i = 0; i < candidates.Count; i++)
        Console.WriteLine($"{i + 1}. {players[candidates[i]].Name} (index {candidates[i]})");

    var choice = Cli.ReadIntInRange("Choice: ", 1, candidates.Count);
    return candidates[choice - 1];
};

Console.WriteLine();
Console.WriteLine("Game start. Press Ctrl+C to quit.");

while (true)
{
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

        if (!session.ApplyAction(session.CurrentPlayerIndex, action, die, out var err) && err is not null)
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
        if (!session.TryCompleteStealWithCard(thiefIndex, cardId, out var stealErr) && stealErr is not null)
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
        if (!session.ApplyAction(victimIndex, stealAction, die, out var stealRespondErr) && stealRespondErr is not null)
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

        if (!session.ApplyAction(responderIndex.Value, action, die, out var err) && err is not null)
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
            if (!session.TryTokenPhaseRecyclePick(currentPlayerIndex, pick, out var recErr) && recErr is not null)
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
                if (!session.TryBanditStashMatchingCard(responderIdx, gid, out var bErr) && bErr is not null)
                    Console.WriteLine(bErr);
            }
            else if (!session.TryBanditPass(responderIdx, out var pErr) && pErr is not null)
                Console.WriteLine(pErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.DoubleStashChoosingCards)
        {
            var ids = controller.ChooseDoubleStashCardIds(view, tp.StashableHandCardsForCurrentPrompt);
            if (!session.TryTokenPhaseDoubleStash(currentPlayerIndex, ids, out var dsErr) && dsErr is not null)
                Console.WriteLine(dsErr);

            continue;
        }

        if (tp?.Step == TokenPhaseStep.StashTrashPickCard)
        {
            var cardId = controller.ChooseStashTrashStashCard(view, tp.StashableHandCardsForCurrentPrompt);
            if (!session.TryTokenPhaseStashTrashPickCard(currentPlayerIndex, cardId, out var stErr) && stErr is not null)
                Console.WriteLine(stErr);

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
            if (!session.ApplyAction(currentPlayerIndex, playerAction, die, out var e1) && e1 is not null)
                Console.WriteLine(e1);
            continue;
        }

        if (playerAction == GameAction.TokenDoubleStashSubmit)
        {
            var ids = controller.ChooseDoubleStashCardIds(view, tp?.StashableHandCardsForCurrentPrompt ?? Array.Empty<(Guid, CardName)>());
            if (!session.TryTokenPhaseDoubleStash(currentPlayerIndex, ids, out var e2) && e2 is not null)
                Console.WriteLine(e2);
            continue;
        }

        if (!session.ApplyAction(currentPlayerIndex, playerAction, die, out var error) && error is not null)
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
    if (!session.ApplyAction(rollPlayerIndex, rollAction, die, out var rollError) && rollError is not null)
        Console.WriteLine(rollError);
}
