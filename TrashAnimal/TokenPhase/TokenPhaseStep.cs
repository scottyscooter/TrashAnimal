namespace TrashAnimal.TokenPhase;

/// <summary>Sub-step while <see cref="GameState.TokenPhase"/> is active.</summary>
public enum TokenPhaseStep
{
    ChoosingNextToken,
    StashTrashChooseBranch,
    StashTrashPickCard,
    DoubleStashChoosingCards,
    BanditAwaitOpponentResponse,
    RecycleChoosingReplacement
}
