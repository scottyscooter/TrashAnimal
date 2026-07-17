using TrashAnimal.TokenPhase;

namespace TrashAnimal;

/// <summary>Token-phase UI state for the viewing player (nested under <see cref="GameView"/>).</summary>
public sealed record TokenPhaseView(
    TokenPhaseStep Step,
    IReadOnlyList<TokenAction> RemainingTokens,
    TokenAction? ActiveToken,
    CardName? BanditRevealedCardName,
    int? BanditCurrentResponderIndex,
    IReadOnlyList<StashableHandCard> StashableHandCardsForCurrentPrompt,
    IReadOnlyList<TokenAction> RecycleReplacementOptions);

/// <summary>One hand card eligible to be stashed in the current TokenPhase prompt.</summary>
public sealed record StashableHandCard(Guid CardId, CardName Name);
