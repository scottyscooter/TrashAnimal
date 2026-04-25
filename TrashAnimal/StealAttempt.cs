namespace TrashAnimal;

/// <summary>
/// In-memory steal attempt (pass / Doggo / Kitteh / card pick). Used from RollPhase (Shiny) and later from TokenPhase (hand steal).
/// </summary>
public sealed class StealAttempt
{
    private int? _thiefIndex;
    private int? _victimIndex;
    private StealTargetZone? _initialZone;

    public bool IsActive => _thiefIndex.HasValue;

    public int? ThiefIndex => _thiefIndex;
    public int? VictimIndex => _victimIndex;
    public StealTargetZone? InitialStealTargetZone => _initialZone;

    public void Begin(int thiefIndex, int victimIndex, StealTargetZone zone)
    {
        _thiefIndex = thiefIndex;
        _victimIndex = victimIndex;
        _initialZone = zone;
    }

    public void BeginStashStealFromShiny(int thiefIndex, int victimIndex) =>
        Begin(thiefIndex, victimIndex, StealTargetZone.Stash);

    public void Clear()
    {
        _thiefIndex = null;
        _victimIndex = null;
        _initialZone = null;
    }

    public static bool AnyOpponentHasStashCards(IReadOnlyList<Player> players, int currentPlayerIndex)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i == currentPlayerIndex)
                continue;
            if (players[i].StashPile.Count > 0)
                return true;
        }

        return false;
    }

    public static IEnumerable<int> GetOpponentIndicesWithNonEmptyStash(IReadOnlyList<Player> players, int thiefIndex)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i == thiefIndex)
                continue;
            if (players[i].StashPile.Count > 0)
                yield return i;
        }
    }

    public IReadOnlyList<GameAction> GetAllowedResponseActions(Player victim)
    {
        var actions = new List<GameAction> { GameAction.StealPass };
        if (victim.Hand.Any(c => c.Name == CardName.Doggo))
            actions.Add(GameAction.StealPlayDoggo);
        if (victim.Hand.Any(c => c.Name == CardName.Kitteh))
            actions.Add(GameAction.StealPlayKitteh);
        return actions;
    }

    public StealPhaseView? BuildPhaseView(GameState sessionState, int viewPlayerIndex, IReadOnlyList<Player> players)
    {
        if (_thiefIndex is null || _victimIndex is null || _initialZone is null)
            return null;

        var thiefIdx = _thiefIndex.Value;
        var vicIdx = _victimIndex.Value;
        var zone = _initialZone.Value;
        IReadOnlyList<StealPickSlot>? pickSlots = null;
        if (sessionState == GameState.AwaitingStealCardPick && viewPlayerIndex == thiefIdx)
            pickSlots = StealPickSlotBuilder.BuildForThief(zone, players[vicIdx]);

        return new StealPhaseView(
            thiefIdx,
            players[thiefIdx].Name,
            vicIdx,
            players[vicIdx].Name,
            zone,
            pickSlots);
    }

    public bool TryRefuseToBlockSteal(int victimIndex, out StealAttemptAftermath aftermath, out string? error)
    {
        error = null;
        aftermath = StealAttemptAftermath.None;
        if (victimIndex != _victimIndex)
        {
            error = "Only the steal victim may pass.";
            return false;
        }

        aftermath = StealAttemptAftermath.AwaitingCardPick;
        return true;
    }

    public bool TryPlayDoggo(
        int victimIndex,
        IList<Player> players,
        IList<Card> discardPile,
        IDrawPile drawPile,
        out StealAttemptAftermath aftermath,
        out string? error)
    {
        error = null;
        aftermath = StealAttemptAftermath.None;
        if (victimIndex != _victimIndex)
        {
            error = "Only the steal victim may play Doggo.";
            return false;
        }

        var victim = players[victimIndex];
        if (!victim.TryRemoveCard(CardName.Doggo, out var doggo))
        {
            error = "No Doggo card in hand.";
            return false;
        }

        discardPile.Add(doggo);

        var drawn = drawPile.DealCards(2);
        victim.AddCards(drawn);
        
        Clear();
        aftermath = StealAttemptAftermath.Completed;
        return true;
    }

    public bool TryPlayKitteh(int victimIndex, IList<Player> players, IList<Card> discardPile, out string? error)
    {
        error = null;
        if (victimIndex != _victimIndex)
        {
            error = "Only the steal victim may play Kitteh.";
            return false;
        }

        var victim = players[victimIndex];
        if (!victim.TryRemoveCard(CardName.Kitteh, out var kitteh))
        {
            error = "No Kitteh card in hand.";
            return false;
        }

        discardPile.Add(kitteh);
        var thief = _thiefIndex!.Value;
        _thiefIndex = victimIndex;
        _victimIndex = thief;
        return true;
    }

    public bool TryCompletePick(int thiefIndex, Guid cardId, IList<Player> players, out string? error)
    {
        error = null;
        if (thiefIndex != _thiefIndex)
        {
            error = "Only the stealing player may complete the steal.";
            return false;
        }

        var zone = _initialZone!.Value;
        var victim = players[_victimIndex!.Value];
        var thief = players[thiefIndex];

        Card stolen;
        if (zone == StealTargetZone.Stash)
        {
            if (!victim.TryRemoveFromStashByCardId(cardId, out var fromStash) || fromStash is null)
            {
                error = "Card is not in the victim's stash.";
                return false;
            }

            stolen = fromStash;
        }
        else
        {
            if (!victim.TryRemoveFromHandByCardId(cardId, out var fromHand) || fromHand is null)
            {
                error = "Card is not in the victim's hand.";
                return false;
            }

            stolen = fromHand;
        }

        thief.AddCards([stolen]);
        Clear();
        return true;
    }
}
