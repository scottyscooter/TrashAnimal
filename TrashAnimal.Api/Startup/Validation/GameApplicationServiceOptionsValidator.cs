using Microsoft.Extensions.Options;
using TrashAnimal.Api.Startup.Options;

namespace TrashAnimal.Api.Startup.Validation;

public sealed class GameApplicationServiceOptionsValidator : IValidateOptions<GameApplicationServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, GameApplicationServiceOptions options)
    {
        var count = options.StartingHandCounts.Count();

        if (count < 2)
            return ValidateOptionsResult.Fail($"{nameof(GameApplicationServiceOptions.StartingHandCounts)} must contain at least 2 values.");

        if (count > 4)
            return ValidateOptionsResult.Fail($"{nameof(GameApplicationServiceOptions.StartingHandCounts)} must not contain more than 4 values.");

        return ValidateOptionsResult.Success;
    }
}