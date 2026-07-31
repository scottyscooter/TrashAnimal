using System.Linq;
using TrashAnimal;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Covers <see cref="OwnStashView"/>'s split between face-down and face-up entries (B2 in
/// <c>.claude/docs/plans/debug-notes-b-card-selection-interaction-model.md</c>). Unlike
/// <see cref="OpponentSummaryView"/>, the viewer's own face-down stash contents are fully identified —
/// face-down-ness is only hidden from opponents, not from the owner.
/// </summary>
public sealed class GameSessionOwnStashViewTests
{
    [Fact]
    public void OwnStash_SplitsFaceDownAndFaceUpEntries_ByFaceUpFlag_NotByCount()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var faceDown1 = new Card(CardName.Nanners);
        var faceDown2 = new Card(CardName.Blammo);
        var faceUp = new Card(CardName.Shiny);
        p0.AddToStash(faceDown1, faceUp: false);
        p0.AddToStash(faceDown2, faceUp: false);
        p0.AddToStash(faceUp, faceUp: true);

        var session = new GameSession(new[] { p0, p1 }, DrawPileMockFactory.CreateEmpty().Object);

        var view = session.GetViewForPlayer(0);

        Assert.Equal(2, view.OwnStash.FaceDownCards.Count);
        Assert.Contains(view.OwnStash.FaceDownCards, c => c.CardId == faceDown1.Id && c.Name == CardName.Nanners);
        Assert.Contains(view.OwnStash.FaceDownCards, c => c.CardId == faceDown2.Id && c.Name == CardName.Blammo);

        Assert.Single(view.OwnStash.FaceUpCards);
        Assert.Equal(faceUp.Id, view.OwnStash.FaceUpCards[0].CardId);
    }

    [Fact]
    public void OwnStash_EmptyStash_ReportsBothListsEmpty()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, DrawPileMockFactory.CreateEmpty().Object);

        var view = session.GetViewForPlayer(0);

        Assert.Empty(view.OwnStash.FaceDownCards);
        Assert.Empty(view.OwnStash.FaceUpCards);
    }
}
