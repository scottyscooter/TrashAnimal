using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class StealPickSlotBuilderTests
{
    [Fact]
    public void Hand_zone_shuffle_reorders_slots_relative_to_hand_order()
    {
        var victim = new Player(1);
        var card0 = new Card(CardName.MmmPie);
        var card1 = new Card(CardName.Feesh);
        var card2 = new Card(CardName.Shiny);
        var card3 = new Card(CardName.Nanners);
        victim.Hand.Add(card0);
        victim.Hand.Add(card1);
        victim.Hand.Add(card2);
        victim.Hand.Add(card3);
        var originalOrder = new[] { card0.Id, card1.Id, card2.Id, card3.Id };

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Hand, victim, new Random(12345));
        var resultingOrder = slots.Select(s => s.CardId).ToArray();

        Assert.NotEqual(originalOrder, resultingOrder);

        // Stable for this seed: re-running the same shuffle sequence from the same seed produces
        // the same permutation, demonstrating the shuffle is deterministic given its RNG input.
        var repeatSlots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Hand, victim, new Random(12345));
        Assert.Equal(resultingOrder, repeatSlots.Select(s => s.CardId).ToArray());
    }

    [Fact]
    public void Hand_zone_shuffle_preserves_every_card_exactly_once()
    {
        var victim = new Player(1);
        var cards = new[]
        {
            new Card(CardName.MmmPie),
            new Card(CardName.Feesh),
            new Card(CardName.Shiny),
            new Card(CardName.Nanners),
            new Card(CardName.Doggo)
        };
        foreach (var card in cards)
            victim.Hand.Add(card);

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Hand, victim, new Random(7));

        Assert.Equal(cards.Length, slots.Count);
        Assert.Equal(
            cards.Select(c => c.Id).OrderBy(id => id),
            slots.Select(s => s.CardId).OrderBy(id => id));
    }

    [Fact]
    public void Hand_zone_all_slots_remain_unrevealed_after_shuffle()
    {
        var victim = new Player(1);
        victim.Hand.Add(new Card(CardName.MmmPie));
        victim.Hand.Add(new Card(CardName.Feesh));
        victim.Hand.Add(new Card(CardName.Shiny));

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Hand, victim, new Random(99));

        Assert.All(slots, s => Assert.Equal(StealPickSlot.UnrevealedLabel, s.ThiefFacingLabel));
    }

    [Fact]
    public void Stash_zone_shuffles_and_face_up_labels_still_resolve_correctly()
    {
        var victim = new Player(1);
        var hiddenA = new Card(CardName.Shiny);
        var hiddenB = new Card(CardName.MmmPie);
        var shownA = new Card(CardName.Nanners);
        var shownB = new Card(CardName.Feesh);
        victim.AddToStash(hiddenA, faceUp: false);
        victim.AddToStash(shownA, faceUp: true);
        victim.AddToStash(hiddenB, faceUp: false);
        victim.AddToStash(shownB, faceUp: true);
        var originalOrder = new[] { hiddenA.Id, shownA.Id, hiddenB.Id, shownB.Id };

        var slots = StealPickSlotBuilder.BuildForThief(StealTargetZone.Stash, victim, new Random(2024));

        Assert.Equal(4, slots.Count);
        Assert.NotEqual(originalOrder, slots.Select(s => s.CardId).ToArray());
        Assert.Equal(StealPickSlot.UnrevealedLabel, slots.Single(s => s.CardId == hiddenA.Id).ThiefFacingLabel);
        Assert.Equal(StealPickSlot.UnrevealedLabel, slots.Single(s => s.CardId == hiddenB.Id).ThiefFacingLabel);
        Assert.Equal(CardName.Nanners.ToString(), slots.Single(s => s.CardId == shownA.Id).ThiefFacingLabel);
        Assert.Equal(CardName.Feesh.ToString(), slots.Single(s => s.CardId == shownB.Id).ThiefFacingLabel);
    }

    [Fact]
    public void Preferred_order_arranges_slots_to_match_supplied_card_id_order()
    {
        var victim = new Player(1);
        var card0 = new Card(CardName.MmmPie);
        var card1 = new Card(CardName.Feesh);
        var card2 = new Card(CardName.Shiny);
        victim.Hand.Add(card0);
        victim.Hand.Add(card1);
        victim.Hand.Add(card2);

        var preferredOrder = new[] { card2.Id, card0.Id, card1.Id };

        var slots = StealPickSlotBuilder.BuildForThief(
            StealTargetZone.Hand, victim, new Random(1), preferredOrder);

        Assert.Equal(preferredOrder, slots.Select(s => s.CardId).ToArray());
    }
}
