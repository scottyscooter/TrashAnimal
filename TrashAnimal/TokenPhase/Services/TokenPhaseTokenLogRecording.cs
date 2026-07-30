using TrashAnimal.GameLog;

namespace TrashAnimal.TokenPhase;

/// <summary>
/// Small stateless helpers for the game-log events every token-resolution step can emit. Shared between
/// <see cref="TokenPhaseTokenResolver"/> (starting a token) and <see cref="TokenPhaseTokenCompletionEngine"/>
/// (finishing/repeating a token) so both stay under this repo's file-length guidance without duplicating
/// event-construction logic.
/// </summary>
internal static class TokenPhaseTokenLogRecording
{
    public static void RecordTokenResolutionStarted(GameSession session, TokenAction token) =>
        session.RecordLogEvent(new TokenResolutionStartedEvent(0, session.TurnNumber, session.CurrentPlayerIndex, token));

    public static void RecordTokenResolvedWithNoEffect(GameSession session, TokenAction token) =>
        session.RecordLogEvent(new TokenResolvedWithNoEffectEvent(0, session.TurnNumber, session.CurrentPlayerIndex, token));

    public static void RecordCardsDrawnPrivately(GameSession session, IReadOnlyList<Card> drawn)
    {
        if (drawn.Count == 0)
            return;

        session.RecordLogEvent(new CardDrawnPrivatelyEvent(
            0,
            session.TurnNumber,
            session.CurrentPlayerIndex,
            drawn.Select(c => c.Id).ToList(),
            drawn.Select(c => c.Name).ToList()));
    }

    public static void RecordCardsStashed(GameSession session, IReadOnlyList<Card> stashed, bool wasFaceUp)
    {
        if (stashed.Count == 0)
            return;

        session.RecordLogEvent(new CardStashedEvent(
            0,
            session.TurnNumber,
            session.CurrentPlayerIndex,
            stashed.Select(c => c.Id).ToList(),
            stashed.Select(c => c.Name).ToList(),
            wasFaceUp));
    }
}
