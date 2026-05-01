namespace TrashAnimal;

/// <summary>One row of the end-of-game scoreboard. Stub totals until scoring rules are implemented.</summary>
public sealed record GameEndScoreLine(int PlayerIndex, string PlayerName, int TotalScore);
