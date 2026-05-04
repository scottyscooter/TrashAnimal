namespace TrashAnimal.Api.Sessions;

public sealed class GameSessionEntry
{
    public GameSessionEntry(GameSession session, Die die)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Die = die ?? throw new ArgumentNullException(nameof(die));
    }

    public GameSession Session { get; }
    public Die Die { get; }
    public int Revision { get; private set; }

    public int IncrementRevision() => ++Revision;
}
