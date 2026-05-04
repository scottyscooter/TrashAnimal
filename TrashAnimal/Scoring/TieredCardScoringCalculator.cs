namespace TrashAnimal;

public sealed class TieredCardScoringCalculator
{
    public IReadOnlyList<int> CalculateTierPoints(
        IReadOnlyList<int> playerCardCounts,
        CardTierPointValues tierPointValues)
    {
        var pointsByPlayer = Enumerable.Repeat(0, playerCardCounts.Count).ToArray();
        if (playerCardCounts.Count == 0)
            return pointsByPlayer;

        var playerIndexGroupsByCount = playerCardCounts
            .Select((count, playerIndex) => new { count, playerIndex })
            .Where(entry => entry.count > 0)
            .GroupBy(entry => entry.count)
            .OrderByDescending(group => group.Key)
            .Take(3)
            .Select(group => group.Select(entry => entry.playerIndex).ToList())
            .ToList();

        for (var rank = 0; rank < playerIndexGroupsByCount.Count; rank++)
        {
            var tierPoints = GetTierPointsForRank(rank, tierPointValues);
            var playerIndexesForRank = playerIndexGroupsByCount[rank];
            var awardedPoints = playerIndexesForRank.Count > 1
                ? Math.Max(0, tierPoints - 1)
                : tierPoints;

            foreach (var playerIndex in playerIndexesForRank)
                pointsByPlayer[playerIndex] += awardedPoints;
        }

        return pointsByPlayer;
    }

    private static int GetTierPointsForRank(int rank, CardTierPointValues tierPointValues) =>
        rank switch
        {
            0 => tierPointValues.HighestTierPoints,
            1 => tierPointValues.MiddleTierPoints,
            2 => tierPointValues.LowestTierPoints,
            _ => 0
        };
}
