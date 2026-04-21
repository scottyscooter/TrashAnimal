namespace TrashAnimal;

/// <summary>
/// Phase 1 die rolling: tokens are unique successful rolls. A bust occurs when a rolled value
/// is already in <see cref="Tokens"/>; the duplicate is not added.
/// </summary>
public sealed class PhaseOneState
{
    private readonly List<TokenAction> _tokens = new();

    public IReadOnlyList<TokenAction> Tokens => _tokens;

    /// <summary>True if the last roll duplicated an existing token.</summary>
    public bool IsBusted { get; private set; }

    /// <summary>The face that was rolled on a bust (same as an existing token).</summary>
    public TokenAction? BustingRoll { get; private set; }

    /// <summary>Remaining mandatory rolls from Yum Yum (must reach 0 before voluntary stop).</summary>
    public bool ForcedRollRemaining { get; private set; }

    public void Reset()
    {
        _tokens.Clear();
        IsBusted = false;
        BustingRoll = null;
        ForcedRollRemaining = false;
    }

    public void AddForcedRoll()
    {
        ForcedRollRemaining = true;
    }

    /// <summary>Roll the die for a token. On bust, state becomes busted and the roll is not added to tokens.</summary>
    public PhaseOneRollResult TryRollForToken(Die die)
    {
        if (IsBusted)
            throw new InvalidOperationException("Cannot roll while busted.");

        var value = die.Roll();
        ForcedRollRemaining = false;

        if (_tokens.Contains(value))
        {
            IsBusted = true;
            BustingRoll = value;
            return PhaseOneRollResult.Busted(value);
        }

        _tokens.Add(value);        

        return PhaseOneRollResult.Success(value);
    }

    /// <summary>Removes bust state after Nanners/Blammo: token list was never given the duplicate.</summary>
    public void ClearBustIgnoringLastRoll()
    {
        if (!IsBusted) throw new InvalidOperationException("Not busted."); // todo: Game state should not be controlling when this action is performed. This is never selected by the player.
        IsBusted = false;
        BustingRoll = null;
    }

    public bool CanVoluntarilyStop()
    {
        return !IsBusted && !ForcedRollRemaining;
    }
}

public readonly struct PhaseOneRollResult
{
    public bool IsBust { get; }
    public TokenAction Rolled { get; }

    private PhaseOneRollResult(bool isBust, TokenAction rolled)
    {
        IsBust = isBust;
        Rolled = rolled;
    }

    public static PhaseOneRollResult Success(TokenAction rolled) => new(false, rolled);
    public static PhaseOneRollResult Busted(TokenAction rolled) => new(true, rolled);
}
