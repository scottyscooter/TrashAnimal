namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// A deterministic die that returns tokens from a predefined sequence, then falls back to
/// <see cref="TokenAction.StashTrash"/> when the sequence is exhausted. Used in integration
/// tests that need predictable die outcomes without relying on a seeded PRNG.
/// </summary>
internal sealed class SequencedDie : Die
{
    private readonly Queue<TokenAction> _sequence;

    internal SequencedDie(params TokenAction[] sequence) : base(Random.Shared)
    {
        _sequence = new Queue<TokenAction>(sequence);
    }

    public override TokenAction Roll() =>
        _sequence.Count > 0 ? _sequence.Dequeue() : TokenAction.StashTrash;
}
