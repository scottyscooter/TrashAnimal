namespace TrashAnimal.TokenPhase;

/// <summary>Hand vs stash eligibility for TokenPhase (instance-based rules).</summary>
public sealed class TokenPhaseCardEligibility
{
    // Cards like Nanners, Blammo, Yumyum are only used for rolling the die so they don't make sense to offer during TokenPhase
    private readonly CardName[] _eligibleCards = new[] { CardName.Shiny, CardName.Feesh, CardName.Doggo, CardName.Kitteh, CardName.MmmPie };

    public bool CanPlayCardForActionDuringTokenPhase(HandEntry entry, bool tokenResolutionStarted)
    {
        if (tokenResolutionStarted && entry.NewlyAdded)
            return false;

        return _eligibleCards.Contains(entry.Card.Name);
    }

    public bool CanOfferCardForStashPrompt(CardName name) =>
        name is not (CardName.Doggo or CardName.Kitteh);
}
