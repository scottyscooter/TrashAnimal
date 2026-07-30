using TrashAnimal.GameLog;
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
        RecordLogEvent(RollPhaseLogEventFactory.ForFeeshRetrieved(playerIndex, TurnNumber, cardFromDiscard.Id, cardFromDiscard.Name));
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
        RecordLogEvent(RollPhaseLogEventFactory.ForShinyStealBegun(playerIndex, TurnNumber, victimIndex));
        return true;
    }

    /// <summary>
    /// Play a Shiny card as a TokenPhase interrupt, stealing from a specific victim's stash.
    /// This method bypasses the ChooseShinyStealVictim delegate for API use.
    /// </summary>
    public bool TryPlayShinyTokenPhaseWithVictimChoice(int playerIndex, int victimIndex, out string? error)
    {
        error = null;
        if (State != GameState.TokenPhase)
        {
            error = "Shiny can only be played as a TokenPhase interrupt while in TokenPhase.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the current player can act during TokenPhase.";
            return false;
        }

        return _tokenPhaseCoordinator.TryPlayShinyWithVictimChoice(victimIndex, out error);
    }

    /// <summary>
    /// Play a Feesh card as a TokenPhase interrupt, retrieving a specific card from the discard pile.
    /// This method bypasses the OnFeeshCardSelection delegate for API use.
    /// </summary>
    public bool TryPlayFeeshTokenPhaseWithCardChoice(int playerIndex, Guid discardCardId, out string? error)
    {
        error = null;
        if (State != GameState.TokenPhase)
        {
            error = "Feesh can only be played as a TokenPhase interrupt while in TokenPhase.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the current player can act during TokenPhase.";
            return false;
        }

        return _tokenPhaseCoordinator.TryPlayFeeshWithCardChoice(discardCardId, out error);
    }

    /// <summary>
    /// Start the Steal token resolution by selecting a victim to steal from their hand — or, when
    /// <paramref name="victimIndex"/> is null, auto-resolve the Steal token because no opponent has any card
    /// in hand to steal (<paramref name="resolvedWithNoEffectToken"/> is set to <see cref="TokenAction.Steal"/>
    /// in that case). This method bypasses the ChooseTokenHandStealVictim delegate for API use. A thin adapter
    /// over <see cref="TokenPhaseCoordinator"/> — same pattern as <see cref="TryPlayShinyWithVictimChoice"/> /
    /// <see cref="TryPlayFeeshWithCardChoice"/> — so RemainingTokens/ActiveToken exhaustion bookkeeping lives
    /// in exactly one place (<see cref="TokenPhase.Services.TokenPhaseTokenResolver"/>) regardless of which
    /// entry point started the Steal token.
    /// </summary>
    public bool TryStartTokenStealWithVictimChoice(int playerIndex, int? victimIndex, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
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
            if (victimIndex is not null)
            {
                error = "No opponent has a card in hand to steal.";
                return false;
            }

            return _tokenPhaseCoordinator.TryResolveStealAutoWithNoTargets(out error, out resolvedWithNoEffectToken);
        }

        if (victimIndex is not int victim || !candidates.Contains(victim))
        {
            error = "Selected victim does not have cards in hand or is not a valid opponent.";
            return false;
        }

        return _tokenPhaseCoordinator.TryStartHandStealWithVictim(victim, out error, out resolvedWithNoEffectToken);
    }
}
