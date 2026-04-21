namespace TrashAnimal;

internal static class Cli
{
    public static int ReadIntInRange(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            var raw = Console.ReadLine();
            if (int.TryParse(raw, out var value) && value >= min && value <= max)
                return value;

            Console.WriteLine($"Please enter a number between {min} and {max}.");
        }
    }

    public static string ReadNonEmptyString(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var s = (Console.ReadLine() ?? string.Empty).Trim();
            if (s.Length > 0)
                return s;
            Console.WriteLine("Please enter a non-empty value.");
        }
    }

    public static bool ReadYesNo(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var s = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (s is "y" or "yes") return true;
            if (s is "n" or "no") return false;
            Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }

    public static void PrintHand(Player player)
    {
        Console.WriteLine($"Hand ({player.Hand.Count}): {string.Join(", ", player.Hand.Select(c => c.Name))}");
    }

    public static void PrintTokens(IReadOnlyList<TokenAction> tokens)
    {
        Console.WriteLine($"Tokens: {(tokens.Count == 0 ? "(none)" : string.Join(", ", tokens))}");
    }
}

