using TrashAnimal.TokenPhase;

namespace TrashAnimal;

/// <summary>Token-phase UI state for the viewing player (nested under <see cref="GameView"/>).</summary>
public sealed record TokenPhaseView(
    TokenPhaseStep Step,
    IReadOnlyList<TokenAction> RemainingTokens,
    TokenAction? ActiveToken,
    CardName? BanditRevealedCardName,
    int? BanditCurrentResponderIndex,
    IReadOnlyList<(Guid CardId, CardName Name)> StashableHandCardsForCurrentPrompt,
    IReadOnlyList<TokenAction> RecycleReplacementOptions);
