namespace TrashAnimal.GameLog;

/// <summary>
/// Builds <see cref="GameLogEvent"/>s for RollPhase card plays (Shiny/Feesh/Nanners/Blammo, and the
/// Steal-token hand steal that mirrors Shiny's stash steal). Shared by both duplicated code paths that
/// can trigger these plays — the RollPhase handler dispatch (<c>GameSession.StealYumRoll.cs</c>'s
/// <c>TryExecuteRollPhaseHandler</c>) and the API's explicit-choice methods
/// (<c>GameSession.ApiSupport.cs</c>) — so their log wording never drifts apart. <see cref="GameLogEvent.SequenceNumber"/>
/// is left at 0 here; <see cref="GameLogRecorder"/> stamps the real value on <c>Record</c>.
/// </summary>
internal static class RollPhaseLogEventFactory
{
    /// <summary>Feesh retrieved <paramref name="retrievedCardName"/> from the discard pile into the acting player's hand.</summary>
    public static GameLogEvent ForFeeshRetrieved(int actingSeat, int turnNumber, Guid retrievedCardId, CardName retrievedCardName) =>
        new CardDrawnPrivatelyEvent(0, turnNumber, actingSeat, new[] { retrievedCardId }, new[] { retrievedCardName });

    /// <summary>Shiny began a stash steal against <paramref name="victimSeat"/>.</summary>
    public static GameLogEvent ForShinyStealBegun(int actingSeat, int turnNumber, int victimSeat) =>
        new StealAttemptedEvent(0, turnNumber, actingSeat, victimSeat, StealTargetZone.Stash, CardName.Shiny);

    /// <summary>A Steal token began a hand steal against <paramref name="victimSeat"/>.</summary>
    public static GameLogEvent ForTokenStealBegun(int actingSeat, int turnNumber, int victimSeat) =>
        new StealAttemptedEvent(0, turnNumber, actingSeat, victimSeat, StealTargetZone.Hand, SourceCard: null);

    /// <summary>A bust-recovery card (Nanners/Blammo) was played, with no target.</summary>
    public static GameLogEvent ForBustRecoveryCardPlayed(int actingSeat, int turnNumber, CardName card) =>
        new CardPlayedEvent(0, turnNumber, actingSeat, card, TargetSeat: null);
}
