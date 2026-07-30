namespace TrashAnimal;

/// <summary>
/// Builds the thief-facing steal-pick projection for a victim's hand or stash. List order here is
/// randomized deliberately, because a victim's hand/stash iteration order leaks acquisition-time
/// information (e.g. the tail slot being the card most recently drawn or stashed) that the thief
/// isn't supposed to know. Shuffling is scoped ONLY to this projection — it is not a general
/// "shuffle hidden collections" policy. Other views (opponent stash face-up cards, victim-picker
/// seat lists, Bandit reveals, discard/Feesh pickers) are already fully public with no ordering
/// leak; don't propagate this pattern to them without confirming a real leak exists first.
/// </summary>
public static class StealPickSlotBuilder
{
    /// <param name="zone">Which of the victim's zones the thief is picking from.</param>
    /// <param name="victim">The player being stolen from.</param>
    /// <param name="shuffle">
    /// RNG used to randomize slot order. Never construct a new <see cref="Random"/> here — callers
    /// thread through a session-scoped instance so behavior stays deterministic/testable.
    /// </param>
    /// <param name="preferredOrder">
    /// When supplied (see <see cref="StealAttempt.ThiefPickOrder"/>), slots are arranged to match
    /// this order instead of being freshly shuffled, so the thief's view stays stable across
    /// re-fetches for the same pending pick. Any slot whose <c>CardId</c> isn't present in
    /// <paramref name="preferredOrder"/> falls back to being shuffled in.
    /// </param>
    public static IReadOnlyList<StealPickSlot> BuildForThief(
        StealTargetZone zone,
        Player victim,
        Random shuffle,
        IReadOnlyList<Guid>? preferredOrder = null)
    {
        var slots = zone == StealTargetZone.Stash
            ? victim.StashPile
                .Select(e => new StealPickSlot(
                    e.Card.Id,
                    e.IsFaceUp ? e.Card.Name.ToString() : StealPickSlot.UnrevealedLabel))
                .ToList()
            : victim.Hand
                .Select(e => new StealPickSlot(e.Card.Id, StealPickSlot.UnrevealedLabel))
                .ToList();

        return preferredOrder is null
            ? ShuffleFisherYates(slots, shuffle)
            : ArrangeByPreferredOrder(slots, preferredOrder, shuffle);
    }

    private static List<StealPickSlot> ShuffleFisherYates(List<StealPickSlot> slots, Random shuffle)
    {
        for (var i = slots.Count - 1; i > 0; i--)
        {
            var j = shuffle.Next(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        return slots;
    }

    private static List<StealPickSlot> ArrangeByPreferredOrder(
        List<StealPickSlot> slots, IReadOnlyList<Guid> preferredOrder, Random shuffle)
    {
        var remainingById = slots.ToDictionary(s => s.CardId);
        var arranged = new List<StealPickSlot>(slots.Count);

        foreach (var cardId in preferredOrder)
        {
            if (remainingById.Remove(cardId, out var slot))
                arranged.Add(slot);
        }

        if (remainingById.Count > 0)
            arranged.AddRange(ShuffleFisherYates(remainingById.Values.ToList(), shuffle));

        return arranged;
    }
}
