namespace TrashAnimal.RollPhase;

/// <summary>
/// Dependencies for <see cref="IGameplayHandler.TryExecute"/>, supplied by <see cref="GameSession"/>.
/// </summary>
public sealed class RollPhasePlayContext
{
    public required IList<Player> Players { get; init; }
    public required int CurrentPlayerIndex { get; init; }
    public required bool IsPhaseOneActive { get; init; }
    public required PhaseOneState PhaseOne { get; init; }
    public required IList<Card> DiscardPile { get; init; }
    public required StealAttempt Steal { get; init; }
    public required GameState CurrentState { get; init; }    
    public Func<int, IReadOnlyList<Card>, Card?>? OnFeeshCardSelection { get; init; }
    public Func<int, IReadOnlyList<int>, int>? ChooseShinyStealVictim { get; init; }
    public required Action<GameState> ApplyState { get; init; }
    public required Action<bool> ApplyCanRoll { get; init; }
    public required Action<bool> ApplyHasStoppedRolling { get; init; }

    /// <summary>Invoked after a stash steal begins from Shiny so the session can record which state to return to when the steal completes.</summary>
    public Action? OnStashStealBegun { get; init; }

    public Player CurrentPlayer => Players[CurrentPlayerIndex];
}
