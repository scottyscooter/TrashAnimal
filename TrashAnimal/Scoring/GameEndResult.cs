namespace TrashAnimal;

public sealed record GameEndResult(IReadOnlyList<GameEndScoreLine> ScoreLines, int WinningPlayerIndex);
