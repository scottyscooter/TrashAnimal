namespace TrashAnimal;

public static class EnumExtensions
{
    extension<TType>(TType t) where TType : struct, Enum
    {
        public static TType GetRandom()
        {
            var rand = new Random();
            var vals = Enum.GetValues<TType>();
            var val = vals.GetValue(rand.Next(vals.Length)) ?? throw new Exception("Unable to obtain enum value.");
            return (TType)val;
        }
    }
}