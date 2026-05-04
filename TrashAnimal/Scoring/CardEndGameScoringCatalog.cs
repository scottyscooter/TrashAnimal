namespace TrashAnimal;

public sealed class CardEndGameScoringCatalog
{
    private readonly Dictionary<CardName, CardTierPointValues> _tierPointValuesByCardName;
    private readonly List<CardName> _rankedCardNames;

    public CardEndGameScoringCatalog()
    {
        _rankedCardNames = new List<CardName>
        {
            CardName.Yumyum,
            CardName.Shiny,
            CardName.Nanners,
            CardName.MmmPie,
            CardName.Feesh
        };

        _tierPointValuesByCardName = new Dictionary<CardName, CardTierPointValues>
        {
            [CardName.Yumyum] = new(4, 2, 0),
            [CardName.Shiny] = new(3, 0, 0),
            [CardName.Nanners] = new(7, 0, 0),
            [CardName.MmmPie] = new(6, 2, 1),
            [CardName.Feesh] = new(5, 3, 1)
        };
    }

    public IReadOnlyList<CardName> RankedCardNames => _rankedCardNames;
    public IReadOnlyDictionary<CardName, CardTierPointValues> TierPointValuesByCardName => _tierPointValuesByCardName;
    public CardName BlammoCardName => CardName.Blammo;
    public int BlammoPointsPerCard => 1;
}
