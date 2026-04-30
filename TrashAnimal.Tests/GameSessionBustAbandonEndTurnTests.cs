using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class GameSessionBustAbandonEndTurnTests
{
    private sealed class SequencedDie : Die
    {
        private readonly Queue<TokenAction> _sequence;

        public SequencedDie(params TokenAction[] sequence) : base(Random.Shared) =>
            _sequence = new Queue<TokenAction>(sequence);

        public override TokenAction Roll() =>
            _sequence.Count > 0 ? _sequence.Dequeue() : TokenAction.StashTrash;
    }

    private sealed class EmptyDrawPile : IDrawPile
    {
        public IEnumerable<Card> DealCards(int count) => Array.Empty<Card>();
    }

    [Fact]
    public void AbandonBust_draws_one_advances_to_next_player_roll_phase_skips_token_phase()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, deck);

        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.PhaseOne.IsBusted);

        var handBefore = p0.Hand.Count;
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out var err), err);

        Assert.Equal(handBefore + 1, p0.Hand.Count);
        Assert.Equal(1, session.CurrentPlayerIndex);
        Assert.Equal(GameState.RollPhase, session.State);
        Assert.NotEqual(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void AbandonBust_empty_draw_pile_still_ends_turn_for_next_player()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new EmptyDrawPile());

        var die = new SequencedDie(TokenAction.Recycle, TokenAction.Recycle);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));

        var handBefore = p0.Hand.Count;
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out var err), err);

        Assert.Equal(handBefore, p0.Hand.Count);
        Assert.Equal(1, session.CurrentPlayerIndex);
        Assert.Equal(GameState.RollPhase, session.State);
    }
}
