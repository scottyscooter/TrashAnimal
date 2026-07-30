using TrashAnimal;
using TrashAnimal.GameLog;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests.GameLog;

/// <summary>
/// Verifies the game log's emission side: RollPhase-handler and API explicit-choice paths emit
/// equivalent events (catches drift between the duplicated paths), a full TokenPhase resolution
/// produces one event per sub-step in increasing <see cref="GameLogEvent.SequenceNumber"/> order,
/// and bust/turn-end/game-end/steal-response events fire at the right points.
/// </summary>
public sealed class GameLogEmissionTests
{
    // --- Die rolls (RollDie) ---

    [Fact]
    public void RollDie_EmitsDieRolledEvent_WithRolledTokenAndNotBusted()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out var err, out _), err);

        var rolled = Assert.Single(session.LogEvents.OfType<DieRolledEvent>());
        Assert.Equal(0, rolled.ActingPlayerSeat);
        Assert.Equal(TokenAction.StashTrash, rolled.Token);
        Assert.False(rolled.WasBust);
    }

    [Fact]
    public void RollDie_DuplicateFace_EmitsDieRolledEvent_WithWasBustTrue()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out var err, out _), err);

        var rolls = session.LogEvents.OfType<DieRolledEvent>().ToList();
        Assert.Equal(2, rolls.Count);
        Assert.False(rolls[0].WasBust);
        Assert.True(rolls[1].WasBust);
        Assert.Equal(TokenAction.Bandit, rolls[1].Token);
    }

    // --- RollPhase-handler path vs ApiSupport explicit-choice path parity ---

    [Fact]
    public void PlayFeesh_DelegatePath_And_ExplicitChoicePath_EmitEquivalentEvents()
    {
        var target = new Card(CardName.MmmPie);

        var p0A = new Player(0, "Alice");
        var p1A = new Player(1, "Bob");
        var sessionDelegatePath = new GameSession(new[] { p0A, p1A }, new Deck());
        sessionDelegatePath.OnFeeshCardSelection = (_, discard) => discard.First(c => c.Name == CardName.MmmPie);
        sessionDelegatePath.DiscardPile.Add(new Card(CardName.MmmPie));
        p0A.Hand.Clear();
        p0A.Hand.Add(new Card(CardName.Feesh));
        Assert.True(sessionDelegatePath.ApplyAction(0, GameAction.PlayFeesh, new Die(), out var err1, out _), err1);

        var p0B = new Player(0, "Alice");
        var p1B = new Player(1, "Bob");
        var sessionApiPath = new GameSession(new[] { p0B, p1B }, new Deck());
        sessionApiPath.DiscardPile.Add(target);
        p0B.Hand.Clear();
        p0B.Hand.Add(new Card(CardName.Feesh));
        Assert.True(sessionApiPath.TryPlayFeeshWithCardChoice(0, target.Id, out var err2), err2);

        var delegateEvent = Assert.Single(sessionDelegatePath.LogEvents.OfType<CardDrawnPrivatelyEvent>());
        var apiEvent = Assert.Single(sessionApiPath.LogEvents.OfType<CardDrawnPrivatelyEvent>());

        Assert.Equal(delegateEvent.ActingPlayerSeat, apiEvent.ActingPlayerSeat);
        Assert.Equal(delegateEvent.CardNames, apiEvent.CardNames);
    }

    [Fact]
    public void PlayShiny_DelegatePath_And_ExplicitChoicePath_EmitEquivalentEvents()
    {
        var p0A = new Player(0, "Alice");
        var p1A = new Player(1, "Bob");
        var sessionDelegatePath = new GameSession(new[] { p0A, p1A }, new Deck());
        sessionDelegatePath.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        p1A.AddToStash(new Card(CardName.Nanners), faceUp: true);
        p0A.Hand.Clear();
        p0A.Hand.Add(new Card(CardName.Shiny));
        Assert.True(sessionDelegatePath.ApplyAction(0, GameAction.PlayShiny, new Die(), out var err1, out _), err1);

        var p0B = new Player(0, "Alice");
        var p1B = new Player(1, "Bob");
        var sessionApiPath = new GameSession(new[] { p0B, p1B }, new Deck());
        p1B.AddToStash(new Card(CardName.Nanners), faceUp: true);
        p0B.Hand.Clear();
        p0B.Hand.Add(new Card(CardName.Shiny));
        Assert.True(sessionApiPath.TryPlayShinyWithVictimChoice(0, victimIndex: 1, out var err2), err2);

        var delegateEvent = Assert.Single(sessionDelegatePath.LogEvents.OfType<StealAttemptedEvent>());
        var apiEvent = Assert.Single(sessionApiPath.LogEvents.OfType<StealAttemptedEvent>());

        Assert.Equal(delegateEvent.ActingPlayerSeat, apiEvent.ActingPlayerSeat);
        Assert.Equal(delegateEvent.TargetSeat, apiEvent.TargetSeat);
        Assert.Equal(delegateEvent.Zone, apiEvent.Zone);
        Assert.Equal(delegateEvent.SourceCard, apiEvent.SourceCard);
    }

    // --- Full TokenPhase resolution: one event per sub-step, increasing SequenceNumber ---

    [Fact]
    public void FullTokenPhaseResolution_StashTrashDraw_EmitsEventsInIncreasingSequenceOrder()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenStashTrash, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.TokenStashTrashDrawOne, die, out _, out _));

        var sequenceNumbers = session.LogEvents.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.OrderBy(n => n), sequenceNumbers);
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);

        Assert.Contains(session.LogEvents, e => e is TurnStoppedRollingEvent);
        Assert.Contains(session.LogEvents, e => e is TokenResolutionStartedEvent { Token: TokenAction.StashTrash });
        Assert.Contains(session.LogEvents, e => e is CardDrawnPrivatelyEvent);
        Assert.Contains(session.LogEvents, e => e is TurnResolvedEvent);

        // TokenResolutionStartedEvent must come before the CardDrawnPrivatelyEvent it produced.
        var tokenResolutionStartedSeq = session.LogEvents.Single(e => e is TokenResolutionStartedEvent).SequenceNumber;
        var cardDrawnSeq = session.LogEvents.Single(e => e is CardDrawnPrivatelyEvent).SequenceNumber;
        Assert.True(tokenResolutionStartedSeq < cardDrawnSeq);
    }

    [Fact]
    public void FullTokenPhaseResolution_DoubleStash_EmitsCardStashedEventWithBothCards()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.DoubleStash).Object;
        var cardA = new Card(CardName.MmmPie);
        var cardB = new Card(CardName.Nanners);
        p0.Hand.Add(cardA);
        p0.Hand.Add(cardB);

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenDoubleStash, die, out _, out _));
        Assert.True(session.TryTokenPhaseDoubleStash(0, new[] { cardA.Id, cardB.Id }, out var err, out _), err);

        var stashedEvent = Assert.Single(session.LogEvents.OfType<CardStashedEvent>());
        Assert.Equal(2, stashedEvent.CardIds.Count);
        Assert.False(stashedEvent.WasFaceUp);
    }

    // --- Bust / turn-end / game-end ---

    [Fact]
    public void AbandonBust_EmitsPlayerBustedEvent_ThenTurnResolvedEvent_ThenTurnEndedEvent()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.PhaseOne.IsBusted);

        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out var err, out _), err);

        var busted = session.LogEvents.OfType<PlayerBustedEvent>().Single();
        var resolved = session.LogEvents.OfType<TurnResolvedEvent>().Single();
        var ended = session.LogEvents.OfType<TurnEndedEvent>().Single();

        Assert.Equal(0, busted.ActingPlayerSeat);
        Assert.True(busted.SequenceNumber < resolved.SequenceNumber);
        Assert.True(resolved.SequenceNumber < ended.SequenceNumber);
    }

    [Fact]
    public void FinalizeGameEnd_EmitsGameEndedEventWithWinningPlayerSeat()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        p0.AddToStash(new Card(CardName.Blammo), faceUp: true);
        var pile = DrawPileMockFactory.CreateWithCards(1).Object;
        var session = new GameSession(new[] { p0, p1 }, pile);
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out _, out _));

        Assert.Equal(GameState.GameEnded, session.State);
        var ended = Assert.Single(session.LogEvents.OfType<GameEndedEvent>());
        Assert.Equal(session.GetGameEndResult().WinningPlayerIndex, ended.WinningPlayerSeat);
    }

    // --- Doggo block vs Kitteh swap vs completed steal: distinct event types ---

    private static (Player p0, Player p1, GameSession session) CreateShinyStealSession()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        return (p0, p1, session);
    }

    [Fact]
    public void Steal_Doggo_EmitsStealBlockedEvent_NotCompletedOrSwapped()
    {
        var (p0, p1, session) = CreateShinyStealSession();
        p1.AddToStash(new Card(CardName.Blammo), faceUp: true);
        p1.Hand.Add(new Card(CardName.Doggo));
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        var die = new Die();

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPlayDoggo, die, out var err, out _), err);

        var blocked = Assert.Single(session.LogEvents.OfType<StealBlockedEvent>());
        Assert.Equal(1, blocked.ActingPlayerSeat);
        Assert.Equal(0, blocked.ThiefSeat);
        Assert.Equal(CardName.Doggo, blocked.BlockingCard);
        Assert.Empty(session.LogEvents.OfType<StealRoleSwappedEvent>());
        Assert.Empty(session.LogEvents.OfType<StealCompletedEvent>());
    }

    [Fact]
    public void Steal_Kitteh_EmitsStealRoleSwappedEvent_NotBlockedOrCompleted()
    {
        var (p0, p1, session) = CreateShinyStealSession();
        p1.AddToStash(new Card(CardName.Nanners), faceUp: true);
        p1.Hand.Add(new Card(CardName.Kitteh));
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        var die = new Die();

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPlayKitteh, die, out var err, out _), err);

        var swapped = Assert.Single(session.LogEvents.OfType<StealRoleSwappedEvent>());
        Assert.Equal(1, swapped.ActingPlayerSeat);
        Assert.Equal(0, swapped.NewVictimSeat);
        Assert.Empty(session.LogEvents.OfType<StealBlockedEvent>());
        Assert.Empty(session.LogEvents.OfType<StealCompletedEvent>());
    }

    [Fact]
    public void Steal_PassThenPick_EmitsStealCompletedEventWithFullCardIdentity()
    {
        var (p0, p1, session) = CreateShinyStealSession();
        var stashed = new Card(CardName.MmmPie);
        p1.AddToStash(stashed, faceUp: false);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        var die = new Die();

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out _, out _));
        Assert.True(session.TryCompleteStealWithCard(0, stashed.Id, out var err, out _), err);

        var completed = Assert.Single(session.LogEvents.OfType<StealCompletedEvent>());
        Assert.Equal(0, completed.ActingPlayerSeat);
        Assert.Equal(1, completed.VictimSeat);
        Assert.Equal(stashed.Id, completed.CardId);
        Assert.Equal(CardName.MmmPie, completed.CardName);
        Assert.Empty(session.LogEvents.OfType<StealBlockedEvent>());
        Assert.Empty(session.LogEvents.OfType<StealRoleSwappedEvent>());
    }

    // --- Shiny/Feesh played as interrupts during TokenPhase (TokenPhaseInterruptCardPlay) must emit the
    // same event shape as the RollPhase-handler path, via the shared RollPhaseLogEventFactory. ---

    [Fact]
    public void PlayShinyDuringTokenPhase_EmitsStealAttemptedEvent_MatchingRollPhaseShinyPlay()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        p1.AddToStash(new Card(CardName.Nanners), faceUp: true);
        p0.Hand.Add(new Card(CardName.Shiny));
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _));

        Assert.True(session.ApplyAction(0, GameAction.PlayShinyTokenPhase, die, out var err, out _), err);

        var attempted = Assert.Single(session.LogEvents.OfType<StealAttemptedEvent>());
        Assert.Equal(0, attempted.ActingPlayerSeat);
        Assert.Equal(1, attempted.TargetSeat);
        Assert.Equal(StealTargetZone.Stash, attempted.Zone);
        Assert.Equal(CardName.Shiny, attempted.SourceCard);
    }

    [Fact]
    public void PlayFeeshDuringTokenPhase_EmitsCardDrawnPrivatelyEvent_MatchingRollPhaseFeeshPlay()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var target = new Card(CardName.MmmPie);
        session.OnFeeshCardSelection = (_, discard) => discard.First(c => c.Name == CardName.MmmPie);
        session.DiscardPile.Add(target);
        p0.Hand.Add(new Card(CardName.Feesh));
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _));

        Assert.True(session.ApplyAction(0, GameAction.PlayFeeshTokenPhase, die, out var err, out _), err);

        var drawn = Assert.Single(session.LogEvents.OfType<CardDrawnPrivatelyEvent>());
        Assert.Equal(0, drawn.ActingPlayerSeat);
        Assert.Equal(target.Id, drawn.CardIds.Single());
        Assert.Equal(CardName.MmmPie, drawn.CardNames.Single());
    }

    [Fact]
    public void PlayMmmPieDuringTokenPhase_EmitsCardPlayedEvent_WithCorrectSequenceNumber()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        p0.Hand.Add(new Card(CardName.MmmPie));
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _));
        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _));

        var beforeCount = session.LogEvents.Count;
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var err, out _), err);

        var played = Assert.Single(session.LogEvents.OfType<CardPlayedEvent>());
        Assert.Equal(0, played.ActingPlayerSeat);
        Assert.Equal(session.TurnNumber, played.TurnNumber);
        Assert.Equal(CardName.MmmPie, played.Card);
        Assert.Null(played.TargetSeat);
        Assert.True(played.SequenceNumber > beforeCount);
    }
}
