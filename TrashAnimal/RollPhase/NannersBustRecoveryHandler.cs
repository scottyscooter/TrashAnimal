namespace TrashAnimal.RollPhase;

public sealed class NannersBustRecoveryHandler : IGameplayHandler
{
    /// <summary>Also reused by <c>GameSession.Views.cs</c> for <see cref="GameAction.PlayBlammo"/>'s
    /// per-hand-card playability projection: Blammo's own handler has no dedicated "not busted" error text
    /// (its <see cref="IGameplayHandler.TryExecute"/> doesn't re-check the bust condition, relying entirely
    /// on the allowed-action gate), but the underlying rule — bust-recovery cards are only usable while
    /// busted — is identical, so this is the closest existing copy rather than inventing new wording.</summary>
    public const string NotBustedReason = "Not busted.";

    public GameAction Action => GameAction.PlayNanners;

    public bool IsActionable(in RollPhaseOfferSnapshot snapshot) =>
        snapshot.IsBustedBranch && snapshot.CurrentPlayer.Hand.Any(e => e.Card.Name == CardName.Nanners);

    public bool TryExecute(RollPhasePlayContext context, int playerIndex, out string? error)
    {
        error = null;
        if (context.CurrentState != GameState.RollPhase)
            throw new InvalidOperationException(
                $"Invalid state for this action. Expected {GameState.RollPhase} but was {context.CurrentState}.");

        if (!context.IsPhaseOneActive)
        {
            error = "RollPhase is not active.";
            return false;
        }

        if (!context.PhaseOne.IsBusted)
        {
            error = NotBustedReason;
            return false;
        }

        if (!context.CurrentPlayer.TryRemoveCard(CardName.Nanners, out var card))
        {
            error = "No Nanners card in hand.";
            return false;
        }

        context.DiscardPile.Add(card);
        context.NotifyBustRecoveryCardDiscarded?.Invoke(card.Id);
        context.PhaseOne.ClearBustIgnoringLastRoll();
        context.ApplyCanRoll(false);
        context.ApplyHasStoppedRolling(true);
        return true;
    }
}
