namespace TrashAnimal.TokenPhase;

// todo refactor this. There is overlap between card functionality from roll phase being used here
internal sealed class TokenPhaseInterruptCardPlay
{
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

        if (!_session.CurrentPlayer.TryRemoveCard(CardName.MmmPie, out var pie))
        {
            error = "No MmmPie in hand.";
            return false;
        }

        _session.DiscardPile.Add(pie);

        state.ResolveTokenTwice = true;
        return true;
    }

    public bool TryPlayShinyTokenPhase(TokenPhaseState state, out string? error)
    {
        error = null;
        var shinyEntry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Shiny);
        if (shinyEntry is null)
        {
            error = "No Shiny in hand.";
            return false;
        }

        if (!_eligibility.CanPlayCardForActionDuringTokenPhase(shinyEntry, state.TokenResolutionStartLocked))
        {
            error = "Shiny cannot be played right now.";
            return false;
        }

        if (!StealAttempt.AnyOpponentHasStashCards(_session.Players, _session.CurrentPlayerIndex))
        {
            error = "No opponent has a card in their stash to steal.";
            return false;
        }

        if (_session.ChooseShinyStealVictim is null)
        {
            error = "No Shiny victim selector configured.";
            return false;
        }

        var candidates = StealAttempt.GetOpponentIndicesWithNonEmptyStash(_session.Players, _session.CurrentPlayerIndex).ToList();
        var victimIndex = _session.ChooseShinyStealVictim(_session.CurrentPlayerIndex, candidates);
        if (!candidates.Contains(victimIndex))
        {
            error = "Shiny victim selection is invalid.";
            return false;
        }

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
        return true;
    }

    public bool TryPlayFeeshTokenPhase(TokenPhaseState state, out string? error)
    {
        error = null;
        var feeshEntry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Feesh);
        if (feeshEntry is null)
        {
            error = "No Feesh in hand.";
            return false;
        }

        if (!_eligibility.CanPlayCardForActionDuringTokenPhase(feeshEntry, state.TokenResolutionStartLocked))
        {
            error = "Feesh cannot be played right now.";
            return false;
        }

        if (_session.DiscardPile.Count == 0)
        {
            error = "No cards in discard pile to retrieve with Feesh.";
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

        if (!_session.CurrentPlayer.TryRemoveCard(CardName.Feesh, out var playedCard))
        {
            error = "No Feesh in hand.";
            return false;
        }

        _session.DiscardPile.Add(playedCard);

        var discardIndex = _session.DiscardPile.FindIndex(c => c.Id == pickedFromDiscard.Id);
        if (discardIndex < 0)
        {
            error = "Could not find selected card in discard pile.";
            return false;
        }

        var cardFromDiscard = _session.DiscardPile[discardIndex];
        _session.DiscardPile.RemoveAt(discardIndex);
        _session.CurrentPlayer.AddCards(new[] { cardFromDiscard }, markReceivedOnOwnerCurrentTurn: true);
        return true;
    }

    public bool CanPlayMmmPie(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.MmmPie);
        return entry is not null && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked);
    }

    public bool CanPlayShinyTokenPhase(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Shiny);
        return entry is not null
               && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked)
               && StealAttempt.AnyOpponentHasStashCards(_session.Players, _session.CurrentPlayerIndex)
               && _session.ChooseShinyStealVictim is not null;
    }

    public bool CanPlayFeeshTokenPhase(TokenPhaseState state)
    {
        var entry = _session.CurrentPlayer.Hand.FirstOrDefault(e => e.Card.Name == CardName.Feesh);
        return entry is not null
               && _eligibility.CanPlayCardForActionDuringTokenPhase(entry, state.TokenResolutionStartLocked)
               && _session.DiscardPile.Count > 0
               && _session.OnFeeshCardSelection is not null;
    }
}
