namespace TrashAnimal;

public sealed record StealPickSlot(Guid CardId, string ThiefFacingLabel)
{
    public const string UnrevealedLabel = "Unrevealed Card";
}
