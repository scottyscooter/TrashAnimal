namespace TrashAnimal;

public static class StealPickSlotBuilder
{
    public static IReadOnlyList<StealPickSlot> BuildForThief(StealTargetZone zone, Player victim)
    {
        if (zone == StealTargetZone.Stash)
        {
            return victim.StashPile
                .Select(e => new StealPickSlot(
                    e.Card.Id,
                    e.IsFaceUp ? e.Card.Name.ToString() : StealPickSlot.UnrevealedLabel))
                .ToList();
        }

        return victim.Hand
            .Select(c => new StealPickSlot(c.Id, StealPickSlot.UnrevealedLabel))
            .ToList();
    }
}
