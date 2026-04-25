namespace TrashAnimal;

public sealed record StealPhaseView(
    int StealingPlayerIndex,
    string StealingPlayerName,
    int VictimIndex,
    string VictimName,
    StealTargetZone InitialStealTargetZone,
    IReadOnlyList<StealPickSlot>? ThiefPickSlots
);
