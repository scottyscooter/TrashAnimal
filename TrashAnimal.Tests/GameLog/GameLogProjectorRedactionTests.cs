using TrashAnimal;
using TrashAnimal.GameLog;
using Xunit;

namespace TrashAnimal.Tests.GameLog;

/// <summary>
/// Verifies <see cref="GameLogProjector"/>'s per-viewer redaction: raw <see cref="GameLogEvent"/>s always
/// carry full identity (redaction happens only here, never at emission/storage — see
/// <c>TrashAnimal/GameLog/GameLogEvent.cs</c>), and every viewer sees the same
/// <see cref="GameLogEntryView.SequenceNumber"/> set with only <see cref="GameLogEntryView.Message"/> differing.
/// </summary>
public sealed class GameLogProjectorRedactionTests
{
    private static readonly IReadOnlyList<Player> Players =
        new[] { new Player(0, "Alice"), new Player(1, "Bob"), new Player(2, "Carol") };

    [Fact]
    public void FaceDownCardStashedEvent_RevealsIdentity_OnlyToActor()
    {
        var cardId = Guid.NewGuid();
        var events = new GameLogEvent[]
        {
            new CardStashedEvent(1, 1, ActingPlayerSeat: 0, new[] { cardId }, new[] { CardName.Feesh }, WasFaceUp: false)
        };

        var actorView = GameLogProjector.BuildForViewer(events, viewerIndex: 0, Players);
        var otherView = GameLogProjector.BuildForViewer(events, viewerIndex: 1, Players);

        Assert.Contains("Feesh", actorView.Single().Message);
        Assert.DoesNotContain("Feesh", otherView.Single().Message);
    }

    [Fact]
    public void FaceUpCardStashedEvent_RevealsIdentity_ToEveryone()
    {
        var cardId = Guid.NewGuid();
        var events = new GameLogEvent[]
        {
            new CardStashedEvent(1, 1, ActingPlayerSeat: 0, new[] { cardId }, new[] { CardName.Blammo }, WasFaceUp: true)
        };

        var actorView = GameLogProjector.BuildForViewer(events, viewerIndex: 0, Players);
        var otherView = GameLogProjector.BuildForViewer(events, viewerIndex: 1, Players);
        var thirdView = GameLogProjector.BuildForViewer(events, viewerIndex: 2, Players);

        Assert.Contains("Blammo", actorView.Single().Message);
        Assert.Contains("Blammo", otherView.Single().Message);
        Assert.Contains("Blammo", thirdView.Single().Message);
    }

    [Fact]
    public void CardDrawnPrivatelyEvent_RevealsIdentity_OnlyToActor()
    {
        var cardId = Guid.NewGuid();
        var events = new GameLogEvent[]
        {
            new CardDrawnPrivatelyEvent(1, 1, ActingPlayerSeat: 0, new[] { cardId }, new[] { CardName.Shiny })
        };

        var actorView = GameLogProjector.BuildForViewer(events, viewerIndex: 0, Players);
        var otherView = GameLogProjector.BuildForViewer(events, viewerIndex: 1, Players);

        Assert.Contains("Shiny", actorView.Single().Message);
        Assert.DoesNotContain("Shiny", otherView.Single().Message);
    }

    [Fact]
    public void StealCompletedEvent_RevealsCardIdentity_OnlyToThief()
    {
        var cardId = Guid.NewGuid();
        var events = new GameLogEvent[]
        {
            new StealCompletedEvent(1, 1, ActingPlayerSeat: 0, VictimSeat: 1, StealTargetZone.Stash, cardId, CardName.MmmPie)
        };

        var thiefView = GameLogProjector.BuildForViewer(events, viewerIndex: 0, Players);
        var victimView = GameLogProjector.BuildForViewer(events, viewerIndex: 1, Players);
        var thirdPartyView = GameLogProjector.BuildForViewer(events, viewerIndex: 2, Players);

        Assert.Contains("MmmPie", thiefView.Single().Message);
        Assert.DoesNotContain("MmmPie", victimView.Single().Message);
        Assert.DoesNotContain("MmmPie", thirdPartyView.Single().Message);

        // Victim and third party get different framing (victim: "from you"; third party: named).
        Assert.Contains("you", victimView.Single().Message);
        Assert.Contains("Bob", thirdPartyView.Single().Message);
    }

    [Fact]
    public void DieRolledEvent_IsPublic_SameMessageToEveryViewer()
    {
        var events = new GameLogEvent[]
        {
            new DieRolledEvent(1, 1, ActingPlayerSeat: 0, TokenAction.StashTrash, WasBust: false)
        };

        var actorView = GameLogProjector.BuildForViewer(events, viewerIndex: 0, Players);
        var otherView = GameLogProjector.BuildForViewer(events, viewerIndex: 1, Players);

        Assert.Contains("StashTrash", otherView.Single().Message);
        Assert.Equal("You rolled a StashTrash.", actorView.Single().Message);
        Assert.Equal("Alice rolled a StashTrash.", otherView.Single().Message);
    }

    [Fact]
    public void EveryViewer_SeesTheSameSequenceNumberSet_OnlyMessageDiffers()
    {
        var events = new GameLogEvent[]
        {
            new TurnStoppedRollingEvent(1, 1, ActingPlayerSeat: 0),
            new CardStashedEvent(2, 1, ActingPlayerSeat: 0, new[] { Guid.NewGuid() }, new[] { CardName.Feesh }, WasFaceUp: false),
            new StealCompletedEvent(3, 1, ActingPlayerSeat: 0, VictimSeat: 1, StealTargetZone.Hand, Guid.NewGuid(), CardName.Kitteh),
            new TurnResolvedEvent(4, 1, ActingPlayerSeat: 0),
        };

        var seatSequenceSets = Enumerable.Range(0, Players.Count)
            .Select(seat => GameLogProjector.BuildForViewer(events, seat, Players).Select(e => e.SequenceNumber).ToList())
            .ToList();

        foreach (var set in seatSequenceSets)
            Assert.Equal(new[] { 1, 2, 3, 4 }, set);
    }

    [Fact]
    public void RawGameLogEvent_AlwaysCarriesFullIdentity_RegardlessOfViewer()
    {
        var cardId = Guid.NewGuid();
        var rawEvent = new CardStashedEvent(1, 1, ActingPlayerSeat: 0, new[] { cardId }, new[] { CardName.Doggo }, WasFaceUp: false);

        // The raw event itself never withholds identity — redaction is purely a projection-time concern.
        Assert.Equal(cardId, rawEvent.CardIds[0]);
        Assert.Equal(CardName.Doggo, rawEvent.CardNames[0]);

        // Confirm this holds independent of which viewer will eventually consume it.
        _ = GameLogProjector.BuildForViewer(new GameLogEvent[] { rawEvent }, viewerIndex: 1, Players);
        Assert.Equal(cardId, rawEvent.CardIds[0]);
        Assert.Equal(CardName.Doggo, rawEvent.CardNames[0]);
    }
}
