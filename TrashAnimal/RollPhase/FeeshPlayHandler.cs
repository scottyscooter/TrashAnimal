namespace TrashAnimal.RollPhase;

public sealed class FeeshPlayHandler : IGameplayHandler
{
    public GameAction Action => GameAction.PlayFeesh;

    public bool IsActionable(in RollPhaseOfferSnapshot snapshot) =>
        snapshot.CurrentPlayer.Hand.Any(e => e.Card.Name == CardName.Feesh)
        && snapshot is { DiscardPileCount: > 0, HasFeeshSelector: true };

    public bool TryExecute(RollPhasePlayContext context, int playerIndex, out string? error)
    {
        error = null;
        if (!RollPhaseActivePlayerRollGuard.TryEnsureRollPhaseActivePlayer(context, playerIndex, out error))
            return false;

        if (context.DiscardPile.Count == 0)
        {
            error = "No cards in discard pile to retrieve with Feesh.";
            return false;
        }

        if (context.OnFeeshCardSelection is null)
        {
            error = "No Feesh card selector configured.";
            return false;
        }

        var pickedFromDiscard = context.OnFeeshCardSelection(context.CurrentPlayerIndex, (IReadOnlyList<Card>)context.DiscardPile);
        if (pickedFromDiscard is null)
        {
            error = "Feesh selection was not provided.";
            return false;
        }

        if (!context.DiscardPile.Any(c => c.Id == pickedFromDiscard.Id))
        {
            error = "Selected card is not in the discard pile.";
            return false;
        }

        if (!context.Players[playerIndex].TryRemoveCard(CardName.Feesh, out var playedCard))
        {
            error = "No Feesh in hand.";
            return false;
        }

        context.DiscardPile.Add(playedCard);

        var discardIndex = -1;
        for (var i = 0; i < context.DiscardPile.Count; i++)
        {
            if (context.DiscardPile[i].Id != pickedFromDiscard.Id)
                continue;
            discardIndex = i;
            break;
        }
        if (discardIndex < 0)
        {
            error = "Could not find selected card in discard pile.";
            context.DiscardPile.RemoveAt(context.DiscardPile.Count - 1);
            context.CurrentPlayer.AddCards(new[] { playedCard }, markReceivedOnOwnerCurrentTurn: true);
            return false;
        }

        var cardFromDiscard = context.DiscardPile[discardIndex];
        context.DiscardPile.RemoveAt(discardIndex);
        context.CurrentPlayer.AddCards(new[] { cardFromDiscard }, markReceivedOnOwnerCurrentTurn: true);
        return true;
    }
}
