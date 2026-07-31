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
/// <param name="PlayableAs">The <see cref="GameAction"/> this card can be played as right now, or null if it
/// is not currently playable. Also the routing key for actually submitting the play.</param>
/// <param name="UnplayableReason">Human-readable explanation for why the card is not currently playable, or
/// null when <paramref name="PlayableAs"/> is set. Left null (with no reason) when it simply isn't the
/// viewer's turn and no interrupt window is open for this card — that state is already visible elsewhere in
/// the view (whose turn it is), so a per-card reason would be redundant.</param>
public sealed record HandCardView(Guid CardId, CardName Name, GameAction? PlayableAs, string? UnplayableReason);

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

/// <summary>The viewing player's own stash pile. Face-down-ness is only hidden from opponents (see
/// <see cref="OpponentSummaryView.StashFaceDownCount"/>) — the owner already knows what they stashed, so
/// their own face-down cards are fully identified here, not just counted.</summary>
public sealed record OwnStashView(
    IReadOnlyList<StashableHandCard> FaceDownCards,
    IReadOnlyList<StashableHandCard> FaceUpCards);

