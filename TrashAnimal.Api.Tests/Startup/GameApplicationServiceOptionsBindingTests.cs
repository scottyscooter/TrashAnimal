using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Startup.Validation;
using Xunit;

namespace TrashAnimal.Api.Tests.Startup;

/// <summary>
/// Regression coverage for a config-binding gotcha: when an options property has a non-empty
/// C# default, <see cref="ConfigurationBinder"/> appends configured collection values on top of
/// that default instead of replacing it. <see cref="GameApplicationServiceOptions.StartingHandCounts"/>
/// previously defaulted to <c>[3, 4, 5, 6]</c>, so binding <c>appsettings.json</c>'s own
/// <c>[3, 4, 5, 6]</c> produced 8 entries and crashed the host on startup via
/// <see cref="GameApplicationServiceOptionsValidator"/>. The default is now empty so the bound
/// config values are the only entries.
/// </summary>
public sealed class GameApplicationServiceOptionsBindingTests
{
    [Fact]
    public void Binding_FourConfiguredValues_ProducesExactlyFourEntries()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:0"] = "3",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:1"] = "4",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:2"] = "5",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:3"] = "6",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<GameApplicationServiceOptions>()
            .BindConfiguration(nameof(GameApplicationServiceOptions));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GameApplicationServiceOptions>>().Value;

        Assert.Equal([3, 4, 5, 6], options.StartingHandCounts);
    }

    [Fact]
    public void Binding_FourConfiguredValues_PassesStartupValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:0"] = "3",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:1"] = "4",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:2"] = "5",
                [$"{nameof(GameApplicationServiceOptions)}:{nameof(GameApplicationServiceOptions.StartingHandCounts)}:3"] = "6",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<GameApplicationServiceOptions>()
            .BindConfiguration(nameof(GameApplicationServiceOptions))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GameApplicationServiceOptions>,
            GameApplicationServiceOptionsValidator>();

        using var provider = services.BuildServiceProvider();

        // Mirrors what StartupValidator does during Host.StartAsync: force eager evaluation.
        var exception = Record.Exception(() =>
            provider.GetRequiredService<IOptions<GameApplicationServiceOptions>>().Value);

        Assert.Null(exception);
    }
}
