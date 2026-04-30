namespace TrashAnimal.TokenPhase;

/// <summary>Mutable state for the active player's token resolution phase.</summary>
public sealed class TokenPhaseState
{
    public TokenPhaseState(IEnumerable<TokenAction> tokensFromRollPhase)
    {
        foreach (var t in tokensFromRollPhase)
        {
            RemainingTokens.Add(t);
            InitialTokensSnapshot.Add(t);
        }        
    }

    public HashSet<TokenAction> RemainingTokens { get; } = new();

    public HashSet<TokenAction> InitialTokensSnapshot { get; } = new();

    public TokenPhaseStep Step { get; set; } = TokenPhaseStep.ChoosingNextToken;

    public TokenAction? ActiveToken { get; set; }

    /// <summary>After the first token is chosen from the pool, hand cards gained this turn cannot be used for card actions.</summary>
    public bool TokenResolutionStartLocked { get; set; }

    public CardName? BanditRevealedName { get; set; }

    public IReadOnlyList<int> BanditOpponentOrder { get; set; } = Array.Empty<int>();

    public int BanditOpponentIndexInOrder { get; set; }

    /// <summary>Mmmpie: when true, after one full pass of <see cref="ActiveToken"/> finishes, run that token again before clearing it.</summary>
    public bool ResolveTokenTwice { get; set; }

    public void ResetBanditWindow()
    {
        BanditRevealedName = null;
        BanditOpponentOrder = Array.Empty<int>();
        BanditOpponentIndexInOrder = 0;
    }
}
