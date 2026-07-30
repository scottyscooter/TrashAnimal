using TrashAnimal.GameLog;
using TrashAnimal.Helpers;

namespace TrashAnimal.TokenPhase;

/// <summary>
/// Owns starting a Steal token's hand-steal with an already-chosen victim (<see cref="StartWithVictim"/>) —
/// used by both the API's explicit-choice path and the CLI, which now drives the same explicit-choice
/// method instead of a delegate callback. Mirrors <see cref="TokenPhaseBanditHandler"/>'s role as the
/// per-token collaborator for its concern; RemainingTokens/ActiveToken exhaustion bookkeeping stays in
/// <see cref="TokenPhaseTokenResolver"/> (the single place that owns "start/resolve a token" for every
/// token type), not here.
/// </summary>
internal sealed class TokenPhaseStealHandler
{
    private readonly GameSession _session;

    public TokenPhaseStealHandler(GameSession session)
    {
        _session = session;
    }

    public bool HasCandidates() =>
        Opponents.GetAllWithNonEmptyHand(_session.Players, _session.CurrentPlayerIndex).Any();

    /// <summary>Starts the hand-steal with an already-chosen victim (the API's explicit-choice path).</summary>
    public bool StartWithVictim(int victimIndex, out string? error)
    {
        error = null;
        var candidates = Opponents.GetAllWithNonEmptyHand(_session.Players, _session.CurrentPlayerIndex).ToList();
        if (!candidates.Contains(victimIndex))
        {
            error = "Selected victim does not have cards in hand or is not a valid opponent.";
            return false;
        }

        BeginSteal(victimIndex);
        return true;
    }

    private void BeginSteal(int victimIndex)
    {
        _session.Steal.Begin(_session.CurrentPlayerIndex, victimIndex, StealTargetZone.Hand);
        _session.ArmStealResumeState(GameState.TokenPhase);
        _session.SetGameState(GameState.AwaitingStealResponse);
        _session.RecordLogEvent(RollPhaseLogEventFactory.ForTokenStealBegun(_session.CurrentPlayerIndex, _session.TurnNumber, victimIndex));
    }
}
