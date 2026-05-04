namespace TrashAnimal;

public sealed class ScoreManager
{
    private readonly CardEndGameScoringCatalog _scoringCatalog;
    private readonly TieredCardScoringCalculator _tieredCardScoringCalculator;

    public ScoreManager()
        : this(new CardEndGameScoringCatalog(), new TieredCardScoringCalculator())
    {
    }

    public ScoreManager(
        CardEndGameScoringCatalog scoringCatalog,
        TieredCardScoringCalculator tieredCardScoringCalculator)
    {
        _scoringCatalog = scoringCatalog ?? throw new ArgumentNullException(nameof(scoringCatalog));
        _tieredCardScoringCalculator = tieredCardScoringCalculator ?? throw new ArgumentNullException(nameof(tieredCardScoringCalculator));
    }

    public GameEndResult ComputeResult(IReadOnlyList<Player> players)
    {
        if (players is null)
            throw new ArgumentNullException(nameof(players));
        if (players.Count == 0)
            throw new ArgumentException("At least one player is required.", nameof(players));

        var totalsByPlayerOrder = new int[players.Count];
        var uniqueCardTypeCountByPlayerOrder = new int[players.Count];
        var totalStashedCardCountByPlayerOrder = new int[players.Count];
        var stashCountsByPlayerAndCard = BuildStashCounts(players, uniqueCardTypeCountByPlayerOrder, totalStashedCardCountByPlayerOrder);

        ApplyTieredPoints(players, stashCountsByPlayerAndCard, totalsByPlayerOrder);
        ApplyBlammoPoints(players, stashCountsByPlayerAndCard, totalsByPlayerOrder);

        var scoreLines = players
            .Select((player, playerOrder) => new GameEndScoreLine(
                player.Index,
                player.Name,
                totalsByPlayerOrder[playerOrder]))
            .OrderBy(line => line.PlayerIndex)
            .ToList();

        var winningPlayerIndex = ResolveWinnerIndex(
            players,
            totalsByPlayerOrder,
            uniqueCardTypeCountByPlayerOrder,
            totalStashedCardCountByPlayerOrder);

        return new GameEndResult(scoreLines, winningPlayerIndex);
    }

    private IReadOnlyList<Dictionary<CardName, int>> BuildStashCounts(
        IReadOnlyList<Player> players,
        int[] uniqueCardTypeCountByPlayerOrder,
        int[] totalStashedCardCountByPlayerOrder)
    {
        var stashCountsByPlayerAndCard = new List<Dictionary<CardName, int>>(players.Count);

        for (var playerOrder = 0; playerOrder < players.Count; playerOrder++)
        {
            var countsByCardName = new Dictionary<CardName, int>();
            foreach (var stashEntry in players[playerOrder].StashPile)
            {
                if (!countsByCardName.TryAdd(stashEntry.Card.Name, 1))
                    countsByCardName[stashEntry.Card.Name]++;
            }

            uniqueCardTypeCountByPlayerOrder[playerOrder] = countsByCardName.Count;
            totalStashedCardCountByPlayerOrder[playerOrder] = players[playerOrder].StashPile.Count;
            stashCountsByPlayerAndCard.Add(countsByCardName);
        }

        return stashCountsByPlayerAndCard;
    }

    private void ApplyTieredPoints(
        IReadOnlyList<Player> players,
        IReadOnlyList<Dictionary<CardName, int>> stashCountsByPlayerAndCard,
        int[] totalsByPlayerOrder)
    {
        foreach (var rankedCardName in _scoringCatalog.RankedCardNames)
        {
            var tierPointValues = _scoringCatalog.TierPointValuesByCardName[rankedCardName];
            var cardCountsByPlayerOrder = new int[players.Count];

            for (var playerOrder = 0; playerOrder < players.Count; playerOrder++)
            {
                if (stashCountsByPlayerAndCard[playerOrder].TryGetValue(rankedCardName, out var count))
                    cardCountsByPlayerOrder[playerOrder] = count;
            }

            var tierPointsByPlayer = _tieredCardScoringCalculator.CalculateTierPoints(cardCountsByPlayerOrder, tierPointValues);
            for (var playerOrder = 0; playerOrder < players.Count; playerOrder++)
                totalsByPlayerOrder[playerOrder] += tierPointsByPlayer[playerOrder];
        }
    }

    private void ApplyBlammoPoints(
        IReadOnlyList<Player> players,
        IReadOnlyList<Dictionary<CardName, int>> stashCountsByPlayerAndCard,
        int[] totalsByPlayerOrder)
    {
        for (var playerOrder = 0; playerOrder < players.Count; playerOrder++)
        {
            if (!stashCountsByPlayerAndCard[playerOrder].TryGetValue(_scoringCatalog.BlammoCardName, out var blammoCount))
                continue;

            totalsByPlayerOrder[playerOrder] += blammoCount * _scoringCatalog.BlammoPointsPerCard;
        }
    }

    private static int ResolveWinnerIndex(
        IReadOnlyList<Player> players,
        IReadOnlyList<int> totalsByPlayerOrder,
        IReadOnlyList<int> uniqueCardTypeCountByPlayerOrder,
        IReadOnlyList<int> totalStashedCardCountByPlayerOrder)
    {
        return players
            .Select((player, playerOrder) => new WinnerCandidate(
                player.Index,
                totalsByPlayerOrder[playerOrder],
                uniqueCardTypeCountByPlayerOrder[playerOrder],
                totalStashedCardCountByPlayerOrder[playerOrder]))
            .OrderByDescending(candidate => candidate.TotalScore)
            .ThenByDescending(candidate => candidate.UniqueCardTypeCount)
            .ThenByDescending(candidate => candidate.TotalStashedCardCount)
            .ThenBy(candidate => candidate.PlayerIndex)
            .First()
            .PlayerIndex;
    }

    private sealed record WinnerCandidate(
        int PlayerIndex,
        int TotalScore,
        int UniqueCardTypeCount,
        int TotalStashedCardCount);
}
