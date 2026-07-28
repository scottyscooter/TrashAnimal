using TrashAnimal.GameLog;

namespace TrashAnimal;

public sealed record GameView(
    GameState State,
    int CurrentPlayerIndex,
    string CurrentPlayerName,
    bool IsBusted,
    bool ForcedRollRemaining,
    IReadOnlyList<TokenAction> PhaseOneTokens,
    IReadOnlyList<HandCardView> HandCards,
    int? YumYumResponderIndex,
    string? YumYumResponderName,
    StealPhaseView? StealPhase,
    TokenPhaseView? TokenPhase,
    IReadOnlyList<OpponentSummaryView> Opponents,
    int DeckCount,
    IReadOnlyList<DiscardCardView> DiscardPile,
    OwnStashView OwnStash,
    IReadOnlyList<GameLogEntryView> Log);

/// <summary>One card in the viewing player's own hand.</summary>
public sealed record HandCardView(Guid CardId, CardName Name);

/// <summary>One card currently in the discard pile (public information).</summary>
public sealed record DiscardCardView(Guid CardId, CardName Name);

/// <summary>Public summary of one opponent's hand size and stash contents (face-up cards are public;
/// hand contents and face-down stash contents are not).</summary>
public sealed record OpponentSummaryView(
    int SeatIndex,
    string Name,
    int HandCount,
    int StashFaceDownCount,
    IReadOnlyList<StashableHandCard> StashFaceUpCards);

/// <summary>The viewing player's own stash pile.</summary>
public sealed record OwnStashView(int FaceDownCount, IReadOnlyList<StashableHandCard> FaceUpCards);

