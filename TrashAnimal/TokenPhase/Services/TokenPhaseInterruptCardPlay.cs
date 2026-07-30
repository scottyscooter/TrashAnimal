using TrashAnimal.GameLog;
using TrashAnimal.Helpers;
using TrashAnimal.RollPhase;

namespace TrashAnimal.TokenPhase;

// todo refactor this. There is overlap between card functionality from roll phase being used here
internal sealed class TokenPhaseInterruptCardPlay
{
    /// <summary>Also reused by <c>GameSession.Views.cs</c>'s per-hand-card playability projection for
    /// <see cref="GameAction.PlayMmmPieTokenPhase"/>, so the wording stays in one place.</summary>
    public const string TokenRepeatPendingReason = "A token repeat is already pending.";

    private readonly GameSession _session;
    private readonly TokenPhaseCardEligibility _eligibility;

    public TokenPhaseInterruptCardPlay(GameSession session, TokenPhaseCardEligibility eligibility)
    {
        _session = session;
        _eligibility = eligibility;
    }

    public bool TryPlayMmmPie(TokenPhaseState state, out string? error)
    {
        error = null;
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.MmmPie);
        if (entry is null)
        {
            error = "No MmmPie in hand.";
            return false;
        }

        if (!_eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked))
        {
            error = "MmmPie cannot be played right now.";
            return false;
        }

        if (state.ResolveTokenTwice)
        {
            error = TokenRepeatPendingReason;
            return false;
        }

        if (!_session.CurrentPlayer.TryRemoveCard(CardName.MmmPie, out var pie))
        {
            error = "No MmmPie in hand.";
            return false;
        }

        _session.DiscardPile.Add(pie);
        _session.RecordLogEvent(RollPhaseLogEventFactory.ForBustRecoveryCardPlayed(_session.CurrentPlayerIndex, _session.TurnNumber, CardName.MmmPie));

        state.ResolveTokenTwice = true;
        return true;
    }

    public bool TryPlayShinyTokenPhase(TokenPhaseState state, out string? error)
    {
        error = null;
        var shinyEntry = FindEligibleShinyEntry(state);
        if (shinyEntry is null)
        {
            error = "Shiny cannot be played right now.";
            return false;
        }

        var candidates = GetShinyCandidates();
        if (candidates.Count == 0)
        {
            error = ShinyPlayHandler.TargetUnavailableReason;
            return false;
        }

        if (_session.ChooseShinyStealVictim is null)
        {
            error = "No Shiny victim selector configured.";
            return false;
        }

        var victimIndex = _session.ChooseShinyStealVictim(_session.CurrentPlayerIndex, candidates);
        if (!candidates.Contains(victimIndex))
        {
            error = "Shiny victim selection is invalid.";
            return false;
        }

        return ApplyShinySteal(victimIndex, out error);
    }

    public bool TryPlayShinyTokenPhaseWithVictimChoice(TokenPhaseState state, int victimIndex, out string? error)
    {
        error = null;
        var shinyEntry = FindEligibleShinyEntry(state);
        if (shinyEntry is null)
        {
            error = "Shiny cannot be played right now.";
            return false;
        }

        var candidates = GetShinyCandidates();
        if (candidates.Count == 0)
        {
            error = ShinyPlayHandler.TargetUnavailableReason;
            return false;
        }

        if (!candidates.Contains(victimIndex))
        {
            error = "Shiny victim selection is invalid.";
            return false;
        }

        return ApplyShinySteal(victimIndex, out error);
    }

    public bool TryPlayFeeshTokenPhase(TokenPhaseState state, out string? error)
    {
        error = null;
        var feeshEntry = FindEligibleFeeshEntry(state);
        if (feeshEntry is null)
        {
            error = "Feesh cannot be played right now.";
            return false;
        }

        if (_session.DiscardPile.Count == 0)
        {
            error = FeeshPlayHandler.NoDiscardCardsReason;
            return false;
        }

        if (_session.OnFeeshCardSelection is null)
        {
            error = "No Feesh card selector configured.";
            return false;
        }

        var pickedFromDiscard = _session.OnFeeshCardSelection(_session.CurrentPlayerIndex, _session.DiscardPile);
        if (pickedFromDiscard is null)
        {
            error = "Feesh selection was not provided.";
            return false;
        }

        if (!_session.DiscardPile.Any(c => c.Id == pickedFromDiscard.Id))
        {
            error = "Selected card is not in the discard pile.";
            return false;
        }

        return ApplyFeeshRetrieve(pickedFromDiscard.Id, out error);
    }

    public bool TryPlayFeeshTokenPhaseWithCardChoice(TokenPhaseState state, Guid discardCardId, out string? error)
    {
        error = null;
        var feeshEntry = FindEligibleFeeshEntry(state);
        if (feeshEntry is null)
        {
            error = "Feesh cannot be played right now.";
            return false;
        }

        if (_session.DiscardPile.Count == 0)
        {
            error = FeeshPlayHandler.NoDiscardCardsReason;
            return false;
        }

        if (!_session.DiscardPile.Any(c => c.Id == discardCardId))
        {
            error = "Selected card is not in the discard pile.";
            return false;
        }

        return ApplyFeeshRetrieve(discardCardId, out error);
    }

    public bool CanPlayMmmPie(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.MmmPie);
        return entry is not null
            && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked)
            && !state.ResolveTokenTwice;
    }

    public bool CanPlayShinyTokenPhase(TokenPhaseState state)
    {
        return FindEligibleShinyEntry(state) is not null && GetShinyCandidates().Count > 0;
    }

    public bool CanPlayFeeshTokenPhase(TokenPhaseState state)
    {
        return FindEligibleFeeshEntry(state) is not null && _session.DiscardPile.Count > 0;
    }

    private HandEntry? FindEligibleShinyEntry(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Shiny);
        return entry is not null && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked)
            ? entry
            : null;
    }

    private HandEntry? FindEligibleFeeshEntry(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Feesh);
        return entry is not null && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked)
            ? entry
            : null;
    }

    private List<int> GetShinyCandidates()
    {
        return Opponents.GetAllWithNonEmptyStash(_session.Players, _session.CurrentPlayerIndex).ToList();
    }

    private bool ApplyShinySteal(int victimIndex, out string? error)
    {
        error = null;
        if (_session.Players[victimIndex].StashPile.Count == 0)
        {
            error = "Selected victim has no cards in stash.";
            return false;
        }

        if (!_session.CurrentPlayer.TryRemoveCard(CardName.Shiny, out var shinyCard))
        {
            error = "No Shiny in hand.";
            return false;
        }

        _session.DiscardPile.Add(shinyCard);
        _session.Steal.BeginStashStealFromShiny(_session.CurrentPlayerIndex, victimIndex);
        _session.ArmStealResumeState(GameState.TokenPhase);
        _session.SetGameState(GameState.AwaitingStealResponse);
        _session.RecordLogEvent(RollPhaseLogEventFactory.ForShinyStealBegun(_session.CurrentPlayerIndex, _session.TurnNumber, victimIndex));
        return true;
    }

    private bool ApplyFeeshRetrieve(Guid discardCardId, out string? error)
    {
        error = null;
        if (!_session.CurrentPlayer.TryRemoveCard(CardName.Feesh, out var playedCard))
        {
            error = "No Feesh in hand.";
            return false;
        }

        _session.DiscardPile.Add(playedCard);

        var discardIndex = _session.DiscardPile.FindIndex(c => c.Id == discardCardId);
        if (discardIndex < 0)
        {
            error = "Could not find selected card in discard pile.";
            return false;
        }

        var cardFromDiscard = _session.DiscardPile[discardIndex];
        _session.DiscardPile.RemoveAt(discardIndex);
        _session.CurrentPlayer.AddCards(new[] { cardFromDiscard }, markReceivedOnOwnerCurrentTurn: true);
        _session.RecordLogEvent(RollPhaseLogEventFactory.ForFeeshRetrieved(_session.CurrentPlayerIndex, _session.TurnNumber, cardFromDiscard.Id, cardFromDiscard.Name));
        return true;
    }
}
