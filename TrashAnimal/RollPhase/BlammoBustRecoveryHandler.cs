namespace TrashAnimal.RollPhase;

public sealed class BlammoBustRecoveryHandler : IGameplayHandler
{
    public GameAction Action => GameAction.PlayBlammo;

    public bool IsActionable(in RollPhaseOfferSnapshot snapshot) =>
        snapshot.IsBustedBranch && snapshot.CurrentPlayer.Hand.Any(e => e.Card.Name == CardName.Blammo);

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

        if (!context.CurrentPlayer.TryRemoveCard(CardName.Blammo, out var card))
        {
            error = "No Blammo card in hand.";
            return false;
        }

        context.DiscardPile.Add(card);
        context.PhaseOne.ClearBustIgnoringLastRoll();
        context.PhaseOne.AddForcedRoll();
        return true;
    }
}
