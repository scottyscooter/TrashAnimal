namespace TrashAnimal.GameLog;

/// <inheritdoc cref="IGameLogRecorder"/>
internal sealed class GameLogRecorder : IGameLogRecorder
{
    private readonly List<GameLogEvent> _events = new();
    private int _nextSequenceNumber = 1;

    public IReadOnlyList<GameLogEvent> Events => _events;

    public void Record(GameLogEvent logEvent)
    {
        var stamped = logEvent with { SequenceNumber = _nextSequenceNumber };
        _nextSequenceNumber++;
        _events.Add(stamped);
    }
}
