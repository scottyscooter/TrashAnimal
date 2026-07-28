namespace TrashAnimal.GameLog;

/// <summary>Append-only, unbounded record of <see cref="GameLogEvent"/>s for a single game. Owned 1:1 by
/// <see cref="GameSession"/>, exactly like <c>_steal</c>/<c>_tokenPhaseCoordinator</c>/<c>_yumYumWindow</c>.
/// No pruning/pagination — a deliberate deferred trade-off (see the game-log-feature plan).</summary>
internal interface IGameLogRecorder
{
    /// <summary>All recorded events in emission order (ascending <see cref="GameLogEvent.SequenceNumber"/>).</summary>
    IReadOnlyList<GameLogEvent> Events { get; }

    /// <summary>Records <paramref name="logEvent"/>, stamping it with the next monotonically increasing
    /// <see cref="GameLogEvent.SequenceNumber"/> (any value already set on it is overwritten).</summary>
    void Record(GameLogEvent logEvent);
}
