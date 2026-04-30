namespace TrashAnimal;

public enum GameAction
{
    RollDie,
    StopRolling,
    AdvanceToResolveTokens,

    PlayShiny,
    PlayFeesh,

    // Bust recovery
    PlayNanners,            // ignore busting roll -> TokenPhase
    PlayBlammo,             // ignore busting roll -> keep rolling -> TokenPhase
    AbandonBust,            // no recovery -> TokenPhase with zero tokens

    // Yum Yum response (opponents)
    YumYumPlay,
    YumYumPass,

    // Steal response (victim of active steal attempt)
    StealPass,
    StealPlayDoggo,
    StealPlayKitteh,

    // TokenPhase (active player unless noted)
    PlayMmmPieTokenPhase,
    PlayShinyTokenPhase,
    PlayFeeshTokenPhase,
    ResolveTokenStashTrash,
    ResolveTokenDoubleStash,
    ResolveTokenDoubleTrash,
    ResolveTokenBandit,
    ResolveTokenSteal,
    ResolveTokenRecycle,
    TokenStashTrashDrawOne,
    TokenStashTrashStashMode,
    TokenDoubleStashSubmit,

    // Bandit response (current opponent only); stash uses TryBanditStashMatchingCard.
    TokenBanditMatchPass,

    // Turn lifecycle
    EndTurn
}

