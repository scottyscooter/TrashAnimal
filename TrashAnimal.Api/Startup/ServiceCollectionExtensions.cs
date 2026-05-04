using Microsoft.Extensions.Options;
using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Startup.Validation;

namespace TrashAnimal.Api.Startup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterOptions(this IServiceCollection services)
    {
        services.AddOptions<GameApplicationServiceOptions>()
            .BindConfiguration(nameof(GameApplicationServiceOptions))
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection RegisterValidators(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<GameApplicationServiceOptions>,
            GameApplicationServiceOptionsValidator>();

        return services;
    }
}
