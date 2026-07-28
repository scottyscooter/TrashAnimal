using TrashAnimal;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class GameSessionBustAbandonEndTurnTests
{
    [Fact]
    public void AbandonBust_draws_one_advances_to_next_player_roll_phase_skips_token_phase()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, deck);

        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;
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
        var session = new GameSession(new[] { p0, p1 }, DrawPileMockFactory.CreateEmpty().Object);

        var die = DieMockFactory.CreateSequenced(TokenAction.Recycle, TokenAction.Recycle).Object;
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));

        var handBefore = p0.Hand.Count;
        Assert.True(session.ApplyAction(0, GameAction.AbandonBust, die, out var err), err);

        Assert.Equal(handBefore, p0.Hand.Count);
        Assert.Equal(1, session.CurrentPlayerIndex);
        Assert.Equal(GameState.RollPhase, session.State);
    }
}
