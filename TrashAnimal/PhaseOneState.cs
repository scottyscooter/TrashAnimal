namespace TrashAnimal;

/// <summary>
/// RollPhase: collect actionable tokens for each unique successful roll of the die. A bust occurs when a rolled value
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

    /// <summary>Remaining mandatory rolls from Yum Yum or Blammo.</summary>
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
    public RollResult TryRollForToken(Die die)
    {
        if (IsBusted)
            throw new InvalidOperationException("Cannot roll while busted.");

        var value = die.Roll();
        ForcedRollRemaining = false;

        if (_tokens.Contains(value))
        {
            IsBusted = true;
            BustingRoll = value;
            return new RollResult(RollStatus.Busted, value);
        }

        _tokens.Add(value);        

        return new RollResult(RollStatus.Success, value);
    }

    /// <summary>Removes bust state after Nanners/Blammo: token list was never given the duplicate.</summary>
    public void ClearBustIgnoringLastRoll()
    {        
        IsBusted = false;
        BustingRoll = null;
    }

    public bool CanVoluntarilyStop()
    {
        return !IsBusted && !ForcedRollRemaining;
    }
}

public readonly struct RollResult(RollStatus status, TokenAction rolled)
{    
    public RollStatus Status { get; } = status;
    public TokenAction Rolled { get; } = rolled;
}

public enum RollStatus
{
    Success,
    Busted
}
