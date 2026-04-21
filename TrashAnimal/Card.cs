using System;

namespace TrashAnimal;

public class Card
{
    public Guid Id { get; private set; }
    public CardName Name { get; private set; }

    public Card(CardName name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }
}
