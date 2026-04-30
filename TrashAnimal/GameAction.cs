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
    /// <summary>Bust only: draw one from the deck, skip token phase, end turn immediately (next player in RollPhase).</summary>
    AbandonBust,

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

