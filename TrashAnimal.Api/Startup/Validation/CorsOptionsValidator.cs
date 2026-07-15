using Microsoft.Extensions.Options;
using TrashAnimal.Api.Startup.Options;

namespace TrashAnimal.Api.Startup.Validation;

public sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        if (!options.AllowedOrigins.Any())
            return ValidateOptionsResult.Fail($"{nameof(CorsOptions.AllowedOrigins)} must contain at least one origin.");

        return ValidateOptionsResult.Success;
    }
}
