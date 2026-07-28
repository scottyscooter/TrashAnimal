namespace TrashAnimal.GameLog;

/// <summary>
/// Base type for the closed hierarchy of domain events recorded during a game. Each event
/// represents one fact, true at one instant (see "Domain Events" in TrashAnimal/CLAUDE.md).
/// Events store only IDs/enums/counts — never full hand/stash snapshots, never a pre-rendered
/// display string. <see cref="SequenceNumber"/> is assigned by <see cref="GameLogRecorder"/> at
/// emission time (monotonically increasing); <see cref="TurnNumber"/> mirrors
/// <c>GameSession.TurnNumber</c> at the moment of emission.
/// </summary>
public abstract record GameLogEvent(int SequenceNumber, int TurnNumber, int ActingPlayerSeat);

/// <summary>A player stashed one or more cards. Card identity is captured raw (redacted only at projection —
/// see <see cref="GameLogProjector"/>). Face-down stashes (<paramref name="WasFaceUp"/> false) are hidden
/// information for everyone but the actor; face-up (Bandit) stashes are public.</summary>
public sealed record CardStashedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    IReadOnlyList<Guid> CardIds,
    IReadOnlyList<CardName> CardNames,
    bool WasFaceUp) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player picked one of their collected roll-phase tokens to begin resolving in
/// TokenPhase. Tokens are public information (visible token trays), so no redaction applies. Note: the
/// token itself was "drawn" earlier, during the roll (see <see cref="DieRolledEvent"/>) — this event marks
/// the start of its resolution, not its acquisition.</summary>
public sealed record TokenResolutionStartedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    TokenAction Token) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player rolled the die during RollPhase. <paramref name="Token"/> is the face rolled;
/// <paramref name="WasBust"/> is true when that face duplicated one already collected this turn (a bust).
/// Die rolls are public information (every player's roll is visible), so no redaction applies.</summary>
public sealed record DieRolledEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    TokenAction Token,
    bool WasBust) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>A card was drawn and revealed face-up to everyone (Bandit reveal).</summary>
public sealed record CardDrawnFaceUpEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    CardName Card) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>A card (or cards) was drawn into a player's hand without being revealed. Identity is captured
/// raw and redacted only at projection — hidden from everyone but the acting player.</summary>
public sealed record CardDrawnPrivatelyEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    IReadOnlyList<Guid> CardIds,
    IReadOnlyList<CardName> CardNames) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>A steal attempt was begun against <paramref name="TargetSeat"/>'s <paramref name="Zone"/>.
/// <paramref name="SourceCard"/> is the card that triggered it (Shiny), or null for a Steal token.</summary>
public sealed record StealAttemptedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int TargetSeat,
    StealTargetZone Zone,
    CardName? SourceCard) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The steal victim (<see cref="GameLogEvent.ActingPlayerSeat"/>) fully blocked the steal by playing Doggo.</summary>
public sealed record StealBlockedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int ThiefSeat,
    CardName BlockingCard) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The steal victim (<see cref="GameLogEvent.ActingPlayerSeat"/>) played Kitteh, swapping thief/victim roles.
/// <paramref name="NewVictimSeat"/> is the seat now on the receiving end (the original thief).</summary>
public sealed record StealRoleSwappedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int NewVictimSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>A steal completed: the thief (<see cref="GameLogEvent.ActingPlayerSeat"/>) took a card from
/// <paramref name="VictimSeat"/>'s <paramref name="Zone"/>. Card identity is captured raw, redacted per
/// viewer role only at projection (thief sees it, victim/third parties do not).</summary>
public sealed record StealCompletedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int VictimSeat,
    StealTargetZone Zone,
    Guid CardId,
    CardName CardName) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>A card was played for its effect where the effect itself isn't already captured by a more
/// specific event (bust-recovery plays: Nanners, Blammo). <paramref name="TargetSeat"/> is null when the
/// play has no target.</summary>
public sealed record CardPlayedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    CardName Card,
    int? TargetSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player busted on a roll.</summary>
public sealed record PlayerBustedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player requested to stop rolling, entering the Yum Yum window. Fires immediately on
/// the stop request — no card count is claimed here because none is known yet (see the event-timing note
/// in the game-log-feature plan).</summary>
public sealed record TurnStoppedRollingEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary><paramref name="OpponentSeat"/> played Yum Yum on the acting player, blocking their stop and
/// forcing another roll.</summary>
public sealed record YumYumForcedRerollEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int OpponentSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player's stop stood and TokenPhase finished resolving their collected tokens (or
/// there were none to resolve). Carries no count of its own — the individual per-token events already
/// reported that as it happened.</summary>
public sealed record TurnResolvedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The acting player's turn officially ended and play passed to the next player (or the game ended).</summary>
public sealed record TurnEndedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);

/// <summary>The game ended. <paramref name="WinningPlayerSeat"/> is the scoring winner.</summary>
public sealed record GameEndedEvent(
    int SequenceNumber,
    int TurnNumber,
    int ActingPlayerSeat,
    int WinningPlayerSeat) : GameLogEvent(SequenceNumber, TurnNumber, ActingPlayerSeat);
