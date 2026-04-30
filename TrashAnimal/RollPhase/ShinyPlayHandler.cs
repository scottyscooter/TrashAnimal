namespace TrashAnimal.RollPhase;

public sealed class ShinyPlayHandler : IGameplayHandler
{
    public GameAction Action => GameAction.PlayShiny;

    public bool IsActionable(in RollPhaseOfferSnapshot snapshot) =>
        snapshot.CurrentPlayer.Hand.Any(e => e.Card.Name == CardName.Shiny)
        && StealAttempt.AnyOpponentHasStashCards((IReadOnlyList<Player>)snapshot.Players, snapshot.CurrentPlayerIndex)
        && snapshot.HasShinyVictimSelector;

    public bool TryExecute(RollPhasePlayContext context, int playerIndex, out string? error)
    {
        error = null;
        if (!RollPhaseActivePlayerRollGuard.TryEnsureRollPhaseActivePlayer(context, playerIndex, out error))
            return false;

        if (!StealAttempt.AnyOpponentHasStashCards((IReadOnlyList<Player>)context.Players, context.CurrentPlayerIndex))
        {
            error = "No opponent has a card in their stash to steal.";
            return false;
        }

        if (context.ChooseShinyStealVictim is null)
        {
            error = "No Shiny victim selector configured.";
            return false;
        }

        var candidates = StealAttempt.GetOpponentIndicesWithNonEmptyStash((IReadOnlyList<Player>)context.Players, context.CurrentPlayerIndex)
            .ToList();
        var victimIndex = context.ChooseShinyStealVictim(context.CurrentPlayerIndex, candidates);
        if (!candidates.Contains(victimIndex))
        {
            error = "Shiny victim selection is invalid.";
            return false;
        }

        if (context.Players[victimIndex].StashPile.Count == 0)
        {
            error = "Selected victim has no cards in stash.";
            return false;
        }

        if (!context.CurrentPlayer.TryRemoveCard(CardName.Shiny, out var shinyCard))
        {
            error = "No Shiny in hand.";
            return false;
        }

        context.DiscardPile.Add(shinyCard);
        context.Steal.BeginStashStealFromShiny(context.CurrentPlayerIndex, victimIndex);
        context.OnStashStealBegun?.Invoke();
        context.ApplyState(GameState.AwaitingStealResponse);
        return true;
    }
}
