namespace TrashAnimal.TokenPhase;

/// <summary>Hand vs stash eligibility for TokenPhase (instance-based rules).</summary>
public sealed class TokenPhaseCardEligibility
{
    public bool CanPlayCardForActionDuringTokenPhase(HandEntry entry, bool tokenResolutionStarted)
    {
        if (entry.Card.Name is CardName.Blammo or CardName.Nanners or CardName.Yumyum)
            return false;

        if (tokenResolutionStarted && entry.NewlyAdded)
            return false;

        return entry.Card.Name is CardName.Shiny or CardName.Feesh or CardName.Doggo or CardName.Kitteh or CardName.MmmPie;
    }

    public bool CanOfferCardForStashPrompt(CardName name) =>
        name is not (CardName.Doggo or CardName.Kitteh);
}
