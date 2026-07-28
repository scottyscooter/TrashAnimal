namespace TrashAnimal.GameLog;

/// <summary>One rendered, per-viewer game log line. A single pre-rendered string per entry (not a
/// structured template+args) — see <see cref="GameLogProjector"/> for how <see cref="Message"/> is
/// redacted per viewer. <see cref="ActingPlayerSeat"/> is included so the frontend can color-code the
/// entry by its actor without parsing the seat out of <see cref="Message"/>.</summary>
public sealed record GameLogEntryView(int SequenceNumber, int TurnNumber, int ActingPlayerSeat, string Message);

/// <summary>
/// Projects the raw <see cref="GameLogEvent"/> stream into a per-viewer <see cref="GameLogEntryView"/> list,
/// enforcing hidden-information redaction. Every viewer sees the same <see cref="GameLogEntryView.SequenceNumber"/>
/// set and the same count/order of entries — only <see cref="GameLogEntryView.Message"/> text differs. Player
/// names are resolved here, from <c>players[seat].Name</c>, never baked into stored events (names are
/// presentation, not domain fact).
/// </summary>
internal static class GameLogProjector
{
    public static IReadOnlyList<GameLogEntryView> BuildForViewer(
        IReadOnlyList<GameLogEvent> events, int viewerIndex, IReadOnlyList<Player> players)
    {
        var entries = new List<GameLogEntryView>(events.Count);
        foreach (var evt in events)
        {
            var message = BuildMessage(evt, viewerIndex, players);
            entries.Add(new GameLogEntryView(evt.SequenceNumber, evt.TurnNumber, evt.ActingPlayerSeat, message));
        }

        return entries;
    }

    private static string BuildMessage(GameLogEvent evt, int viewerIndex, IReadOnlyList<Player> players) => evt switch
    {
        CardStashedEvent e => BuildCardStashedMessage(e, viewerIndex, players),
        TokenResolutionStartedEvent e => $"{Actor(e.ActingPlayerSeat, viewerIndex, players)} picked a {e.Token} token to resolve.",
        DieRolledEvent e => BuildDieRolledMessage(e, viewerIndex, players),
        CardDrawnFaceUpEvent e => $"{Actor(e.ActingPlayerSeat, viewerIndex, players)} revealed {e.Card}.",
        CardDrawnPrivatelyEvent e => BuildCardDrawnPrivatelyMessage(e, viewerIndex, players),
        StealAttemptedEvent e => BuildStealAttemptedMessage(e, viewerIndex, players),
        StealBlockedEvent e => BuildStealBlockedMessage(e, viewerIndex, players),
        StealRoleSwappedEvent e => BuildStealRoleSwappedMessage(e, viewerIndex, players),
        StealCompletedEvent e => BuildStealCompletedMessage(e, viewerIndex, players),
        CardPlayedEvent e => BuildCardPlayedMessage(e, viewerIndex, players),
        PlayerBustedEvent e => $"{Actor(e.ActingPlayerSeat, viewerIndex, players)} busted!",
        TurnStoppedRollingEvent e => $"{Actor(e.ActingPlayerSeat, viewerIndex, players)} stopped rolling.",
        YumYumForcedRerollEvent e => BuildYumYumForcedRerollMessage(e, viewerIndex, players),
        TurnResolvedEvent e => BuildTurnResolvedMessage(e, viewerIndex, players),
        TurnEndedEvent e => BuildTurnEndedMessage(e, viewerIndex, players),
        GameEndedEvent e => BuildGameEndedMessage(e, viewerIndex, players),
        _ => throw new NotSupportedException($"Unhandled GameLogEvent type: {evt.GetType().Name}")
    };

    private static string BuildCardStashedMessage(CardStashedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        if (e.WasFaceUp || e.ActingPlayerSeat == viewerIndex)
            return $"{actor} stashed {JoinCardNames(e.CardNames)}.";

        return $"{actor} stashed {CardCountPhrase(e.CardIds.Count)}.";
    }

    private static string BuildDieRolledMessage(DieRolledEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        return e.WasBust
            ? $"{actor} rolled a {e.Token} — busted!"
            : $"{actor} rolled a {e.Token}.";
    }

    private static string BuildCardDrawnPrivatelyMessage(CardDrawnPrivatelyEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        if (e.ActingPlayerSeat == viewerIndex)
            return $"{actor} drew {JoinCardNames(e.CardNames)}.";

        return $"{actor} drew {CardCountPhrase(e.CardIds.Count)}.";
    }

    private static string BuildStealAttemptedMessage(StealAttemptedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        var zone = ZoneWord(e.Zone);
        var possessiveTarget = e.TargetSeat == viewerIndex ? "your" : $"{players[e.TargetSeat].Name}'s";
        return $"{actor} attempted to steal from {possessiveTarget} {zone}.";
    }

    private static string BuildStealBlockedMessage(StealBlockedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        var possessiveThief = e.ThiefSeat == viewerIndex ? "your" : $"{players[e.ThiefSeat].Name}'s";
        return $"{actor} played {e.BlockingCard} to block {possessiveThief} steal.";
    }

    private static string BuildStealRoleSwappedMessage(StealRoleSwappedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        var target = e.NewVictimSeat == viewerIndex ? "you" : players[e.NewVictimSeat].Name;
        return $"{actor} played Kitteh, turning the tables on {target}.";
    }

    private static string BuildStealCompletedMessage(StealCompletedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        if (viewerIndex == e.ActingPlayerSeat)
        {
            var victim = players[e.VictimSeat].Name;
            return $"{actor} stole {e.CardName} from {victim}.";
        }

        if (viewerIndex == e.VictimSeat)
            return $"{actor} stole a card from you.";

        var victimName = players[e.VictimSeat].Name;
        return $"{actor} stole a card from {victimName}.";
    }

    private static string BuildCardPlayedMessage(CardPlayedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        if (e.TargetSeat is null)
            return $"{actor} played {e.Card}.";

        var target = e.TargetSeat == viewerIndex ? "you" : players[e.TargetSeat.Value].Name;
        return $"{actor} played {e.Card} on {target}.";
    }

    private static string BuildYumYumForcedRerollMessage(YumYumForcedRerollEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var opponent = Actor(e.OpponentSeat, viewerIndex, players);
        var isRollerViewing = e.ActingPlayerSeat == viewerIndex;
        var roller = isRollerViewing ? "you" : players[e.ActingPlayerSeat].Name;                
        return $"Keep rolling! {opponent} played a Yum Yum on {roller}.";
    }

    private static string BuildTurnResolvedMessage(TurnResolvedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        var possessive = e.ActingPlayerSeat == viewerIndex ? "your" : "their";
        return $"{actor} finished {possessive} turn.";
    }

    private static string BuildTurnEndedMessage(TurnEndedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        var actor = Actor(e.ActingPlayerSeat, viewerIndex, players);
        var possessive = e.ActingPlayerSeat == viewerIndex ? "your" : "their";
        return $"{actor} ended {possessive} turn.";
    }

    private static string BuildGameEndedMessage(GameEndedEvent e, int viewerIndex, IReadOnlyList<Player> players)
    {
        if (e.WinningPlayerSeat == viewerIndex)
            return "You won the game!";

        return $"{players[e.WinningPlayerSeat].Name} won the game!";
    }

    private static string Actor(int seat, int viewerIndex, IReadOnlyList<Player> players) =>
        seat == viewerIndex ? "You" : players[seat].Name;

    private static string ZoneWord(StealTargetZone zone) => zone == StealTargetZone.Hand ? "hand" : "stash";

    private static string CardCountPhrase(int count) => count == 1 ? "1 card" : $"{count} cards";

    private static string JoinCardNames(IReadOnlyList<CardName> names) => string.Join(", ", names);
}
