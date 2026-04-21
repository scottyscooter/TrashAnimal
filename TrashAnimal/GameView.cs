namespace TrashAnimal;

public sealed record GameView(
    GameState State,
    int CurrentPlayerIndex,
    string CurrentPlayerName,
    bool IsBusted,
    bool ForcedRollRemaining,
    IReadOnlyList<TokenAction> PhaseOneTokens,
    IReadOnlyList<CardName> HandCardNames,
    int? YumYumResponderIndex,
    string? YumYumResponderName
);

