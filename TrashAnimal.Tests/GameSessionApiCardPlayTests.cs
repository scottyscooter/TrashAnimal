using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Verifies that <c>GetAllowedActionsForPlayer</c> surfaces <c>PlayFeesh</c> and <c>PlayShiny</c>
/// when no delegate selectors are configured — the pattern used by the API, which drives those
/// plays through <c>TryPlayFeeshWithCardChoice</c> and <c>TryPlayShinyWithVictimChoice</c> instead.
///
/// A previous fix attempt tied <c>HasFeeshSelector</c>/<c>HasShinyVictimSelector</c> to whether
/// the delegate was non-null, which would have silently hidden both actions in API mode.
/// These tests pin that contract so the same regression cannot recur.
/// </summary>
public sealed class GameSessionApiCardPlayTests
{
    private sealed class SequencedDie(params TokenAction[] sequence) : Die(Random.Shared)
    {
        private readonly Queue<TokenAction> _rolls = new(sequence);
        public override TokenAction Roll() => _rolls.Count > 0 ? _rolls.Dequeue() : TokenAction.StashTrash;
    }

    /// <summary>
    /// Creates a two-player session with NO delegate selectors — the API mode baseline.
    /// </summary>
    private static (Player p0, Player p1, GameSession session) CreateApiModeSession()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        // Intentionally leave OnFeeshCardSelection and ChooseShinyStealVictim null
        // to mirror how the API uses the session.
        return (p0, p1, session);
    }

    private static void BustWithTwoIdenticalRolls(GameSession session, Die die)
    {
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.False(session.PhaseOne.IsBusted);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.PhaseOne.IsBusted);
    }

    [Fact]
    public void PlayFeesh_appears_in_allowed_actions_without_delegate_when_discard_has_eligible_card()
    {
        var (p0, _, session) = CreateApiModeSession();
        var card = new Card(CardName.MmmPie);
        session.DiscardPile.Add(card);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Feesh));

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.Contains(GameAction.PlayFeesh, allowed);
    }

    [Fact]
    public void PlayShiny_appears_in_allowed_actions_without_delegate_when_opponent_has_stash()
    {
        var (p0, p1, session) = CreateApiModeSession();
        p1.AddToStash(new Card(CardName.MmmPie), faceUp: true);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.Contains(GameAction.PlayShiny, allowed);
    }

    [Fact]
    public void TryPlayFeeshWithCardChoice_retrieves_selected_card_without_delegate()
    {
        var (p0, _, session) = CreateApiModeSession();
        var target = new Card(CardName.MmmPie);
        session.DiscardPile.Add(target);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Feesh));

        var succeeded = session.TryPlayFeeshWithCardChoice(0, target.Id, out var error);

        Assert.True(succeeded, error);
        Assert.Contains(target, p0.Hand.Select(e => e.Card));
        Assert.DoesNotContain(session.DiscardPile, c => c.Id == target.Id);
        Assert.Contains(session.DiscardPile, c => c.Name == CardName.Feesh);
    }

    [Fact]
    public void TryPlayShinyWithVictimChoice_begins_steal_without_delegate()
    {
        var (p0, p1, session) = CreateApiModeSession();
        var stashed = new Card(CardName.MmmPie);
        p1.AddToStash(stashed, faceUp: true);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var succeeded = session.TryPlayShinyWithVictimChoice(0, victimIndex: 1, out var error);

        Assert.True(succeeded, error);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.Equal(0, session.StealThiefIndex);
        Assert.Equal(1, session.StealVictimIndex);
    }

    [Fact]
    public void Bust_empty_discard_nanners_feesh_not_offered_without_delegate()
    {
        var (p0, _, session) = CreateApiModeSession();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        BustWithTwoIdenticalRolls(session, die);
        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));

        var afterNanners = session.GetAllowedActionsForPlayer(0);

        Assert.DoesNotContain(GameAction.PlayFeesh, afterNanners);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterNanners);
    }

    [Fact]
    public void Bust_discard_has_card_nanners_feesh_offered_and_retrieves_card_without_delegate()
    {
        var (p0, _, session) = CreateApiModeSession();
        var target = new Card(CardName.MmmPie);
        session.DiscardPile.Add(target);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.Recycle, TokenAction.Recycle);
        BustWithTwoIdenticalRolls(session, die);
        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));

        var afterNanners = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, afterNanners);

        var succeeded = session.TryPlayFeeshWithCardChoice(0, target.Id, out var error);
        Assert.True(succeeded, error);
        Assert.Contains(target, p0.Hand.Select(e => e.Card));
    }
}
