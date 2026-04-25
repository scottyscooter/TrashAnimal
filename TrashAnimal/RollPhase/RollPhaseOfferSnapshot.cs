namespace TrashAnimal.RollPhase;

/// <summary>
/// Read-only inputs for whether a roll-phase card action should appear for the current player.
/// </summary>
public readonly record struct RollPhaseOfferSnapshot(
    Player CurrentPlayer,
    int CurrentPlayerIndex,
    IReadOnlyList<Player> Players,
    bool IsBustedBranch,
    int DiscardPileCount,
    bool HasFeeshSelector,
    bool HasShinyVictimSelector);
