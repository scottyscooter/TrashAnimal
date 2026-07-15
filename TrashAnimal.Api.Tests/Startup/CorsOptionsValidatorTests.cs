using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Startup.Validation;
using Xunit;

namespace TrashAnimal.Api.Tests.Startup;

public sealed class CorsOptionsValidatorTests
{
    private readonly CorsOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithAtLeastOneOrigin_Succeeds()
    {
        var options = new CorsOptions { AllowedOrigins = ["http://localhost:5173"] };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithNoOrigins_Fails()
    {
        var options = new CorsOptions { AllowedOrigins = [] };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
    }
}
