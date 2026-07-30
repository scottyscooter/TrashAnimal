using TrashAnimal.RollPhase;
using TrashAnimal.TokenPhase;

namespace TrashAnimal;

/// <summary>
/// Builds the <see cref="HandCardView.PlayableAs"/>/<see cref="HandCardView.UnplayableReason"/> pair for one
/// hand card, per the ranked-reason contract documented on <see cref="HandCardView"/>.
///
/// <para>This is a thin per-card projection over eligibility <em>sources of truth</em> that already exist
/// elsewhere (<see cref="GameSession.GetAllowedActionsForPlayer"/>, which itself delegates to
/// <see cref="RollPhaseGameplayHandlerRegistry"/>/<see cref="IGameplayHandler.IsActionable"/> for RollPhase
/// and to <see cref="TokenPhaseAllowedActionsProvider"/> for TokenPhase). It does not re-derive any
/// eligibility rule — the only new logic here is (a) mapping a <see cref="CardName"/> to the
/// <see cref="GameAction"/> it would use in the current phase, and (b) ranking which of several possibly-true
/// reasons is the most specific one to report.</para>
/// </summary>
internal static class HandCardPlayabilityProjector
{
    internal const string NewlyDrawnReason = "Cards drawn during your current turn cannot be played.";

    /// <summary>Yumyum has no phase-mapped play action for the active player at all — <see
    /// cref="MapCardToActionForState"/> always returns null for it, since it is never something the
    /// active player plays on their own turn. Without this dedicated reason, that null would fall
    /// through to the generic "cannot be played during the X phase" rank-2 wording, which is false:
    /// Yumyum can't be played during either phase by the active player, full stop, regardless of
    /// which phase they're in — the real constraint is who may play it and when, not the phase.</summary>
    internal const string YumyumReason = "Can only be played when an opponent chooses to stop rolling.";

    /// <summary>
    /// Maps a card to the <see cref="GameAction"/> it would be played as in <paramref name="state"/>, or null
    /// if this card has no play action in that phase at all (either because it's never phase-playable this
    /// way, e.g. Doggo/Kitteh/Yumyum which are interrupt-only and handled by their own dedicated UI, or
    /// because it belongs to the other roll-vs-token phase).
    /// </summary>
    private static GameAction? MapCardToActionForState(CardName cardName, GameState state) => (cardName, state) switch
    {
        (CardName.Shiny, GameState.RollPhase) => GameAction.PlayShiny,
        (CardName.Shiny, GameState.TokenPhase) => GameAction.PlayShinyTokenPhase,
        (CardName.Feesh, GameState.RollPhase) => GameAction.PlayFeesh,
        (CardName.Feesh, GameState.TokenPhase) => GameAction.PlayFeeshTokenPhase,
        (CardName.Nanners, GameState.RollPhase) => GameAction.PlayNanners,
        (CardName.Blammo, GameState.RollPhase) => GameAction.PlayBlammo,
        (CardName.MmmPie, GameState.TokenPhase) => GameAction.PlayMmmPieTokenPhase,
        _ => null
    };

    private static string PhaseDisplayName(GameState state) => state switch
    {
        GameState.RollPhase => "roll phase",
        GameState.TokenPhase => "token resolve phase",
        _ => state.ToString()
    };

    /// <summary>Rank-3 text: the action has an entry in the current phase's rule set, but is not currently
    /// actionable (no valid target, a blocking condition like "not busted" or "a repeat is already pending",
    /// etc). Reuses the exact wording each handler already uses for the same rejection rather than inventing
    /// new copy — see the constants referenced below.</summary>
    private static string GetTargetUnavailableReason(GameAction action) => action switch
    {
        GameAction.PlayShiny or GameAction.PlayShinyTokenPhase => ShinyPlayHandler.TargetUnavailableReason,
        GameAction.PlayFeesh or GameAction.PlayFeeshTokenPhase => FeeshPlayHandler.NoDiscardCardsReason,
        GameAction.PlayNanners => NannersBustRecoveryHandler.NotBustedReason,
        // Blammo's own handler has no dedicated "not busted" text (see NannersBustRecoveryHandler.NotBustedReason's
        // doc comment) — same underlying rule, so reuse Nanners' copy instead of inventing new wording.
        GameAction.PlayBlammo => NannersBustRecoveryHandler.NotBustedReason,
        GameAction.PlayMmmPieTokenPhase => TokenPhaseInterruptCardPlay.TokenRepeatPendingReason,
        _ => $"{action} is not available right now."
    };

    /// <summary>
    /// Projects one <see cref="HandEntry"/> into its <see cref="HandCardView"/>. <paramref name="allowedActionsForViewer"/>
    /// must be <see cref="GameSession.GetAllowedActionsForPlayer"/>'s result for the same player, computed once
    /// per view build (not once per card) by the caller.
    /// </summary>
    internal static HandCardView Build(
        HandEntry entry,
        GameState state,
        int playerIndex,
        int currentPlayerIndex,
        IReadOnlyList<GameAction> allowedActionsForViewer)
    {
        var cardId = entry.Card.Id;
        var cardName = entry.Card.Name;

        // Not the viewer's turn (or the current state is one of the dedicated-UI interrupt windows — Yum
        // Yum response, Bandit response, Steal response — that already have their own affordance outside the
        // hand fan): no per-card reason, the view's other fields already communicate whose turn it is.
        var isActivePlayerNormalTurn = playerIndex == currentPlayerIndex
            && (state == GameState.RollPhase || state == GameState.TokenPhase);
        if (!isActivePlayerNormalTurn)
            return new HandCardView(cardId, cardName, null, null);

        // Rank 1: drawn this turn, most specific — wins over every other reason.
        if (entry.NewlyAdded)
            return new HandCardView(cardId, cardName, null, NewlyDrawnReason);

        // Yumyum's unplayability isn't phase-shaped (see YumyumReason's doc comment) — special-case
        // it before the generic phase-name rank-2 wording, which would otherwise misattribute the
        // reason to "the phase" rather than "who's allowed to play it."
        if (cardName == CardName.Yumyum)
            return new HandCardView(cardId, cardName, null, YumyumReason);

        var action = MapCardToActionForState(cardName, state);

        // Rank 2: this card's play action doesn't apply during the current phase at all.
        if (action is null)
            return new HandCardView(cardId, cardName, null, $"{cardName} cannot be played during the {PhaseDisplayName(state)}.");

        if (allowedActionsForViewer.Contains(action.Value))
            return new HandCardView(cardId, cardName, action, null);

        // Rank 3: the action applies in this phase, but isn't currently actionable (no valid target, a
        // blocking condition, etc) — per GameSession.GetAllowedActionsForPlayer, the real source of truth.
        return new HandCardView(cardId, cardName, null, GetTargetUnavailableReason(action.Value));
    }
}
