using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class GameSessionDeckExhaustionTests
{
    private sealed class SequencedDie : Die
    {
        private readonly Queue<TokenAction> _sequence;

        public SequencedDie(params TokenAction[] sequence) : base(Random.Shared) =>
            _sequence = new Queue<TokenAction>(sequence);

        public override TokenAction Roll() =>
            _sequence.Count > 0 ? _sequence.Dequeue() : TokenAction.StashTrash;
    }

    private sealed class CountingDrawPile : IDrawPile
    {
        private readonly List<Card> _stock;

        public CountingDrawPile(int count, CardName name = CardName.Nanners)
        {
            _stock = Enumerable.Range(0, count).Select(_ => new Card(name)).ToList();
        }

        public int GetDeckCount() => _stock.Count;

        public IEnumerable<Card> DealCards(int count)
        {
            if (count <= 0)
                yield break;

            var n = Math.Min(count, _stock.Count);
            for (var i = 0; i < n; i++)
            {
                var card = _stock[0];
                _stock.RemoveAt(0);
                yield return card;
            }
        }
    }

    [Fact]
    public void Last_card_on_bust_abandon_ends_game_discards_hands_does_not_advance_turn()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var pile = new CountingDrawPile(1);
        var session = new GameSession(new[] { p0, p1 }, pile);

        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.PhaseOne.IsBusted);

        var aliceHandBefore = p0.Hand.Count;
        var bobHandBefore = p1.Hand.Count;
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out var err), err);

        Assert.Equal(GameState.GameEnded, session.State);
        Assert.Equal(0, session.CurrentPlayerIndex);
        Assert.Empty(p0.Hand);
        Assert.Empty(p1.Hand);
        Assert.Equal(aliceHandBefore + 1 + bobHandBefore, session.DiscardPile.Count);
    }

    [Fact]
    public void Last_two_cards_on_double_trash_token_ends_game_after_EndTurn()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var pile = new CountingDrawPile(2);
        var session = new GameSession(new[] { p0, p1 }, pile);

        var die = new SequencedDie(TokenAction.DoubleTrash);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out _));
        Assert.True(session.ApplyAction(1, GameAction.YumYumPass, die, out _));

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _));
        Assert.Equal(GameState.TokenPhase, session.State);

        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenDoubleTrash, die, out _));
        Assert.Equal(GameState.TurnEnd, session.State);
        Assert.Equal(0, pile.GetDeckCount());

        session.EndTurn();

        Assert.Equal(GameState.GameEnded, session.State);
        Assert.Empty(p0.Hand);
        Assert.Empty(p1.Hand);
    }

    [Fact]
    public void ApplyAction_rejected_when_GameEnded()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var pile = new CountingDrawPile(1);
        var session = new GameSession(new[] { p0, p1 }, pile);
        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out _));
        Assert.Equal(GameState.GameEnded, session.State);

        Assert.False(session.ApplyAction(0, GameAction.RollDie, die, out var err));
        Assert.Equal("The game has ended.", err);
    }

    [Fact]
    public void GetGameEndScoreSummary_throws_before_GameEnded()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new CountingDrawPile(10));
        Assert.Throws<InvalidOperationException>(() => session.GetGameEndScoreSummary());
    }

    [Fact]
    public void GetGameEndScoreSummary_returns_stub_lines_after_GameEnded()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var pile = new CountingDrawPile(1);
        var session = new GameSession(new[] { p0, p1 }, pile);
        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out _));

        var summary = session.GetGameEndScoreSummary();
        Assert.Equal(2, summary.Count);
        Assert.All(summary, line => Assert.Equal(0, line.TotalScore));
        Assert.Contains(summary, line => line.PlayerName == "Alice");
        Assert.Contains(summary, line => line.PlayerName == "Bob");
    }
}
