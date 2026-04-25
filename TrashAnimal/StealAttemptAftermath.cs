namespace TrashAnimal;

/// <summary>How <see cref="StealAttempt"/> state changed after a steal action.</summary>
public enum StealAttemptAftermath
{
    /// <summary>Victim played Kitteh; thief and victim swapped; still awaiting response.</summary>
    None,

    /// <summary>Victim passed; thief must pick a card.</summary>
    AwaitingCardPick,

    /// <summary>Steal chain ended (Doggo block or successful pick); steal fields cleared.</summary>
    Completed
}
