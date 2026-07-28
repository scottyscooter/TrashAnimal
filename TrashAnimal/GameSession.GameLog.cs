using TrashAnimal.GameLog;

namespace TrashAnimal;

public sealed partial class GameSession
{
    /// <summary>1-based count of turns started this game, incremented in <see cref="BeginTurn"/>.</summary>
    public int TurnNumber { get; private set; }

    /// <summary>Records a game log event. Callers assemble the event with a placeholder <c>SequenceNumber</c>
    /// (e.g. 0) — <see cref="GameLogRecorder"/> stamps the real, monotonically increasing value.</summary>
    public void RecordLogEvent(GameLogEvent logEvent) => _logRecorder.Record(logEvent);

    /// <summary>Raw, unredacted event stream for this game. Internal — redaction only happens at
    /// <see cref="GameLogProjector"/>, never by withholding data here; do not expose this to callers
    /// outside the domain project without redacting first.</summary>
    internal IReadOnlyList<GameLogEvent> LogEvents => _logRecorder.Events;
}
