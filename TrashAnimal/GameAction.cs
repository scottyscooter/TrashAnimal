namespace TrashAnimal;

public enum GameAction
{
    RollDie,
    StopRolling,
    AdvanceToResolveTokens,

    PlayShiny,
    PlayFeesh,

    // Bust recovery
    PlayNanners,            // ignore busting roll -> Phase 2
    PlayBlammo,             // ignore busting roll -> keep rolling -> Phase 2
    AbandonBust,            // no recovery -> Phase 2 with zero tokens

    // Yum Yum response (opponents)
    YumYumPlay,
    YumYumPass,

    // Turn lifecycle
    EndTurn
}

