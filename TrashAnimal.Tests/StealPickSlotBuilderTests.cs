using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class StealPickSlotBuilderTests
{
    [Fact]
    public void Stash_face_up_shows_name_face_down_unrevealed()
    {
        var victim = new Player(1);
        var hidden = new Card(CardName.Shiny);
        var shown = new Card(CardName.Nanners);
        victim.AddToStash(hidden, faceUp: false);
        victim.AddToStash(shown, faceUp: true);

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Stash, victim);
        Assert.Equal(2, slots.Count);
        Assert.Equal(StealPickSlot.UnrevealedLabel, slots.Single(s => s.CardId == hidden.Id).ThiefFacingLabel);
        Assert.Equal(CardName.Nanners.ToString(), slots.Single(s => s.CardId == shown.Id).ThiefFacingLabel);
    }

    [Fact]
    public void Hand_all_slots_unrevealed()
    {
        var victim = new Player(1);
        victim.Hand.Add(new Card(CardName.MmmPie));
        victim.Hand.Add(new Card(CardName.Feesh));

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Hand, victim);
        Assert.Equal(2, slots.Count);
        Assert.All(slots, s => Assert.Equal(StealPickSlot.UnrevealedLabel, s.ThiefFacingLabel));
    }
}
