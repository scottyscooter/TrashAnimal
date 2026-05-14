using TrashAnimal.Helpers;

namespace TrashAnimal;

public sealed partial class GameSession
{
    /// <summary>
    /// Play a Feesh card to retrieve a specific card from the discard pile.
    /// This method bypasses the OnFeeshCardSelection delegate for API use.
    /// </summary>
    public bool TryPlayFeeshWithCardChoice(int playerIndex, Guid discardCardId, out string? error)
    {
        error = null;
        if (State != GameState.RollPhase)
        {
            error = "Feesh can only be played during RollPhase.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the current player can play cards during RollPhase.";
            return false;
        }

        if (DiscardPile.Count == 0)
        {
            error = "No cards in discard pile to retrieve with Feesh.";
            return false;
        }

        var selectedCard = DiscardPile.FirstOrDefault(c => c.Id == discardCardId);
        if (selectedCard is null)
        {
            error = "Selected card is not in the discard pile.";
            return false;
        }

        if (!CurrentPlayer.TryRemoveCard(CardName.Feesh, out var playedCard))
        {
            error = "No Feesh in hand.";
            return false;
        }

        DiscardPile.Add(playedCard);

        var discardIndex = DiscardPile.FindIndex(c => c.Id == discardCardId);
        if (discardIndex < 0)
        {
            error = "Could not find selected card in discard pile.";
            DiscardPile.RemoveAt(DiscardPile.Count - 1);
            CurrentPlayer.AddCards(new[] { playedCard }, markReceivedOnOwnerCurrentTurn: true);
            return false;
        }

        var cardFromDiscard = DiscardPile[discardIndex];
        DiscardPile.RemoveAt(discardIndex);
        CurrentPlayer.AddCards(new[] { cardFromDiscard }, markReceivedOnOwnerCurrentTurn: true);
        return true;
    }

    /// <summary>
    /// Play a Shiny card to steal from a specific victim's stash.
    /// This method bypasses the ChooseShinyStealVictim delegate for API use.
    /// </summary>
    public bool TryPlayShinyWithVictimChoice(int playerIndex, int victimIndex, out string? error)
    {
        error = null;
        if (State != GameState.RollPhase)
        {
            error = "Shiny can only be played during RollPhase.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the current player can play cards during RollPhase.";
            return false;
        }

        if (!Opponents.GetAllWithNonEmptyStash(Players, CurrentPlayerIndex).Any())
        {
            error = "No opponent has a card in their stash to steal.";
            return false;
        }

        var candidates = Opponents.GetAllWithNonEmptyStash(Players, CurrentPlayerIndex).ToList();
        if (!candidates.Contains(victimIndex))
        {
            error = "Selected victim does not have cards in stash or is not a valid opponent.";
            return false;
        }

        if (_players[victimIndex].StashPile.Count == 0)
        {
            error = "Selected victim has no cards in stash.";
            return false;
        }

        if (!CurrentPlayer.TryRemoveCard(CardName.Shiny, out var shinyCard))
        {
            error = "No Shiny in hand.";
            return false;
        }

        DiscardPile.Add(shinyCard);
        _steal.BeginStashStealFromShiny(CurrentPlayerIndex, victimIndex);
        ArmStealResumeState(GameState.RollPhase);
        State = GameState.AwaitingStealResponse;
        return true;
    }

    /// <summary>
    /// Start the Steal token resolution by selecting a victim to steal from their hand.
    /// This method bypasses the ChooseTokenHandStealVictim delegate for API use.
    /// </summary>
    public bool TryStartTokenStealWithVictimChoice(int playerIndex, int victimIndex, out string? error)
    {
        error = null;
        if (State != GameState.TokenPhase)
        {
            error = "Token steal can only be resolved during TokenPhase.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the current player can resolve their tokens.";
            return false;
        }

        var candidates = Opponents.GetAllWithNonEmptyHand(Players, CurrentPlayerIndex).ToList();
        if (candidates.Count == 0)
        {
            error = "No opponent has a card in hand to steal.";
            return false;
        }

        if (!candidates.Contains(victimIndex))
        {
            error = "Selected victim does not have cards in hand or is not a valid opponent.";
            return false;
        }

        if (_players[victimIndex].Hand.Count == 0)
        {
            error = "Selected victim has an empty hand.";
            return false;
        }

        _steal.Begin(CurrentPlayerIndex, victimIndex, StealTargetZone.Hand);
        ArmStealResumeState(GameState.TokenPhase);
        State = GameState.AwaitingStealResponse;
        return true;
    }
}
