namespace TrashAnimal;

public class Die
{
    private readonly Random _random;

    public Die(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public virtual TokenAction Roll()
    {
        var values = Enum.GetValues<TokenAction>();
        return values[_random.Next(values.Length)];
    }
}
