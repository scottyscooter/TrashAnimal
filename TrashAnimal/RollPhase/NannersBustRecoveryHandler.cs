namespace TrashAnimal.RollPhase;

public sealed class NannersBustRecoveryHandler : IGameplayHandler
{
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
            error = "Not busted.";
            return false;
        }

        if (!context.CurrentPlayer.TryRemoveCard(CardName.Nanners, out var card))
        {
            error = "No Nanners card in hand.";
            return false;
        }

        context.DiscardPile.Add(card);
        context.PhaseOne.ClearBustIgnoringLastRoll();
        context.ApplyCanRoll(false);
        context.ApplyHasStoppedRolling(true);
        return true;
    }
}
