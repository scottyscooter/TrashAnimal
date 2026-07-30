using TrashAnimal.GameLog;
using TrashAnimal.TokenPhase;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Regression tests for the MmmPie-repeated Steal token strand: an MmmPie repeat of Steal is
/// server-initiated (no in-flight client request could have carried a victim), so it must park in
/// <see cref="TokenPhaseStep.StealChoosingVictim"/> and wait to be asked again, instead of calling
/// the CLI-only <c>ChooseTokenHandStealVictim</c> delegate (always null in an API-driven game) and
/// silently swallowing the resulting error. See
/// <c>.claude/docs/plans/steal-token-mmmpie-repeat-fix.md</c>.
/// </summary>
public sealed class TokenPhaseStealRepeatTests
{
    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInTokenPhase(
        Die die, IDrawPile drawPile)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out var rollError, out _);
        Assert.True(session.PhaseOne.Tokens.Count > 0, rollError);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        return (p0, p1, session);
    }

    private static (Player p0, Player p1, Player p2, GameSession session) CreateThreePlayerApiModeSessionInTokenPhase(
        Die die, IDrawPile drawPile)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var p2 = new Player(2, "Cara");
        var session = new GameSession(new[] { p0, p1, p2 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out var rollError, out _);
        Assert.True(session.PhaseOne.Tokens.Count > 0, rollError);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        PassEntireYumYumWindow(session, die);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        return (p0, p1, p2, session);
    }

    private static void PassEntireYumYumWindow(GameSession session, Die die)
    {
        while (session.State == GameState.AwaitingYumYum)
        {
            var responder = session.GetCurrentYumYumResponderIndex();
            Assert.NotNull(responder);
            Assert.True(session.ApplyAction(responder.Value, GameAction.YumYumPass, die, out var err, out _), err);
        }
    }

    /// <summary>Sets up: MmmPie played, first Steal against <paramref name="firstVictimIndex"/> started and
    /// completed normally (victim passes, thief picks the card), landing exactly on the MmmPie repeat
    /// cascade. Returns the picked card's id for assertions.</summary>
    private static void PlayMmmPieAndCompleteFirstSteal(GameSession session, Die die, Player thief, Player firstVictim, int firstVictimIndex)
    {
        thief.Hand.Add(new Card(CardName.MmmPie));
        Assert.True(session.ApplyAction(thief.Index, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        var started = session.TryStartTokenStealWithVictimChoice(thief.Index, victimIndex: firstVictimIndex, out var startError, out var startFizzle);
        Assert.True(started, startError);
        Assert.Null(startFizzle);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        Assert.True(session.ApplyAction(firstVictimIndex, GameAction.StealPass, die, out var passError, out _), passError);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);
        var stolenCardId = firstVictim.Hand[0].Card.Id;

        Assert.True(session.TryCompleteStealWithCard(thief.Index, stolenCardId, out var pickError, out _), pickError);
    }

    [Fact]
    public void MmmPieRepeatOfSteal_AfterFirstStealCompletes_ParksInStealChoosingVictimStep()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh));
        p1.Hand.Add(new Card(CardName.Yumyum)); // p1 still has a card after the first steal completes

        PlayMmmPieAndCompleteFirstSteal(session, die, p0, p1, firstVictimIndex: 1);

        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);
        Assert.Equal(TokenAction.Steal, session.GetViewForPlayer(0).TokenPhase!.ActiveToken);
        Assert.NotEqual(GameState.TurnEnd, session.State);
    }

    [Fact]
    public void MmmPieRepeatOfSteal_AfterDoggoBlock_ParksInStealChoosingVictimStep()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Doggo));
        p1.Hand.Add(new Card(CardName.Feesh)); // remains after Doggo is played — keeps candidates non-empty

        p0.Hand.Add(new Card(CardName.MmmPie));
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        var started = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var startError, out var startFizzle);
        Assert.True(started, startError);
        Assert.Null(startFizzle);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        Assert.True(session.TryStealPlayDoggo(1, out var doggoError, out _), doggoError);

        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);
        Assert.Equal(TokenAction.Steal, session.GetViewForPlayer(0).TokenPhase!.ActiveToken);
        Assert.NotEqual(GameState.TurnEnd, session.State);
    }

    [Fact]
    public void MmmPieRepeatOfSteal_AfterKittehReversalThenCompletion_ParksInStealChoosingVictimStep()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Kitteh));
        p1.Hand.Add(new Card(CardName.Feesh));
        p0.Hand.Add(new Card(CardName.Yumyum)); // target for the reversed thief (p1) to pick after the swap

        p0.Hand.Add(new Card(CardName.MmmPie));
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        var started = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var startError, out var startFizzle);
        Assert.True(started, startError);
        Assert.Null(startFizzle);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        // p1 (original victim) plays Kitteh: thief/victim roles swap (thief=1, victim=0).
        Assert.True(session.TryStealPlayKitteh(1, out var kittehError), kittehError);
        Assert.Equal(1, session.StealThiefIndex);
        Assert.Equal(0, session.StealVictimIndex);

        // New victim (p0, the original thief) passes; new thief (p1) picks a card from p0's hand.
        Assert.True(session.ApplyAction(0, GameAction.StealPass, die, out var passError, out _), passError);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);
        var stolenCardId = p0.Hand[0].Card.Id;

        Assert.True(session.TryCompleteStealWithCard(1, stolenCardId, out var pickError, out _), pickError);

        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);
        Assert.Equal(TokenAction.Steal, session.GetViewForPlayer(0).TokenPhase!.ActiveToken);
        Assert.NotEqual(GameState.TurnEnd, session.State);
    }

    [Fact]
    public void StealChoosingVictim_AllowsOnlyResolveTokenSteal()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh));
        p1.Hand.Add(new Card(CardName.Yumyum));

        PlayMmmPieAndCompleteFirstSteal(session, die, p0, p1, firstVictimIndex: 1);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);

        var currentAllowed = session.GetAllowedActionsForPlayer(0);
        Assert.Equal(new[] { GameAction.ResolveTokenSteal }, currentAllowed);

        var nonActiveAllowed = session.GetAllowedActionsForPlayer(1);
        Assert.Empty(nonActiveAllowed);
    }

    [Fact]
    public void StealChoosingVictim_AcceptingVictim_ReopensStealAgainstThatVictim()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, p2, session) = CreateThreePlayerApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh)); // first-steal target; hand emptied by the first pick
        p2.Hand.Add(new Card(CardName.Doggo)); // repeat target — must be offered StealPlayDoggo afterward

        PlayMmmPieAndCompleteFirstSteal(session, die, p0, p1, firstVictimIndex: 1);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);

        var reopened = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 2, out var error, out var resolvedWithNoEffectToken);

        Assert.True(reopened, error);
        Assert.Null(resolvedWithNoEffectToken);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.Equal(2, session.StealVictimIndex);

        var victimAllowed = session.GetAllowedActionsForPlayer(2);
        Assert.Contains(GameAction.StealPlayDoggo, victimAllowed);
    }

    [Fact]
    public void MmmPieRepeatOfSteal_CanTargetADifferentVictimThanTheFirstSteal()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, p2, session) = CreateThreePlayerApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh));
        p2.Hand.Add(new Card(CardName.Yumyum));

        PlayMmmPieAndCompleteFirstSteal(session, die, p0, p1, firstVictimIndex: 1);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, session.GetViewForPlayer(0).TokenPhase!.Step);

        var succeeded = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 2, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Null(resolvedWithNoEffectToken);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.Equal(2, session.StealVictimIndex);
    }

    [Fact]
    public void MmmPieRepeatOfSteal_WithNoRemainingCandidates_StillFizzles()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh)); // exactly one card — after the first steal, p1's hand is empty

        PlayMmmPieAndCompleteFirstSteal(session, die, p0, p1, firstVictimIndex: 1);

        Assert.Equal(GameState.TurnEnd, session.State);
        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Steal token, but no opponent had a card"));
    }

    [Fact]
    public void MmmPie_CannotBeStackedWhileRepeatPending()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh));
        p0.Hand.Add(new Card(CardName.MmmPie));
        p0.Hand.Add(new Card(CardName.MmmPie));

        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var firstError, out _), firstError);

        Assert.DoesNotContain(GameAction.PlayMmmPieTokenPhase, session.GetAllowedActionsForPlayer(0));

        // Not offered in allowed actions, so a direct re-attempt is rejected by ApplyAction's allowed-action
        // gate before ever reaching TokenPhaseInterruptCardPlay.TryPlayMmmPie's own guard — both layers now
        // agree stacking is illegal while a repeat is pending.
        var succeeded = session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var secondError, out _);
        Assert.False(succeeded);
        Assert.NotNull(secondError);
    }

    [Fact]
    public void MmmPie_RemainsPlayableOnADifferentTokenAfterRepeatConsumed()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal, TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out _, out _);
        session.ApplyAction(0, GameAction.RollDie, die, out _, out _);
        Assert.Equal(2, session.PhaseOne.Tokens.Count);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);
        Assert.Equal(GameState.TokenPhase, session.State);

        p1.Hand.Add(new Card(CardName.Feesh)); // fizzles the Steal repeat once consumed, freeing MmmPie again
        p0.Hand.Add(new Card(CardName.MmmPie));
        p0.Hand.Add(new Card(CardName.MmmPie));

        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        var started = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var startError, out _);
        Assert.True(started, startError);
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var passError, out _), passError);
        var stolenCardId = p1.Hand[0].Card.Id;
        Assert.True(session.TryCompleteStealWithCard(0, stolenCardId, out var pickError, out var cascadeFizzle), pickError);

        // Zero remaining hand candidates for p1 => the repeat fizzles instead of parking, consuming
        // ResolveTokenTwice and returning to ChoosingNextToken with StashTrash still available.
        Assert.Equal(TokenAction.Steal, cascadeFizzle);
        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Equal(TokenPhaseStep.ChoosingNextToken, session.GetViewForPlayer(0).TokenPhase!.Step);

        // MmmPie must still be playable on the different, still-remaining StashTrash token — this must not
        // regress as a side effect of blocking stacked repeats.
        Assert.Contains(GameAction.PlayMmmPieTokenPhase, session.GetAllowedActionsForPlayer(0));
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var secondMmmPieError, out _), secondMmmPieError);
    }
}
