using TrashAnimal;

var die = new Die();

Console.WriteLine("TrashAnimal (CLI)");
Console.WriteLine();

var playerCount = Cli.ReadIntInRange("How many players (2-4)? ", 2, 4);

var players = new List<Player>(playerCount);
var controllers = new List<IPlayerController>(playerCount);

for (var i = 0; i < playerCount; i++)
{
    var name = Cli.ReadNonEmptyString($"Enter name for player {i + 1}: ");
    var isComputer = Cli.ReadYesNo($"Is {name} a computer player? (y/n) ");

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

var session = new GameSession(players, new PhaseTwoNoop())
{
    OnShinyFeeshPlayed = static (_, card) =>
    {
        // Effects are stubbed for now.
        Console.WriteLine($"[effect hook] Played {card} (effects stubbed).");
    }
};

Console.WriteLine();
Console.WriteLine("Game start. Press Ctrl+C to quit.");

while (true)
{
    if (session.State == GameState.TurnEnd)
    {
        Console.WriteLine();
        Console.WriteLine($"-- End of {session.CurrentPlayer.Name}'s turn --");
        Console.WriteLine($"Phase 2 tokens: {(session.LastPhaseTwoTokens.Count == 0 ? "(none)" : string.Join(", ", session.LastPhaseTwoTokens))}");

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

    if (session.State == GameState.AwaitingYumYum)
    {
        var responderIndex = session.GetCurrentYumYumResponderIndex();
        if (responderIndex is null)
            throw new InvalidOperationException("AwaitingYumYum but no responder.");

        var responder = players[responderIndex.Value];
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

    // Phase 1 (active player)
    var activeIndex = session.CurrentPlayerIndex;
    var active = players[activeIndex];
    var controller = controllers[activeIndex];
    var allowedActions = session.GetAllowedActionsForPlayer(activeIndex);
    var viewForActive = session.GetViewForPlayer(activeIndex);

    Console.WriteLine();
    Console.WriteLine($"-- {active.Name}'s Phase 1 --");

    var chosen = controller.ChooseAction(viewForActive, allowedActions);
    if (!session.ApplyAction(activeIndex, chosen, die, out var error) && error is not null)
        Console.WriteLine(error);
}

