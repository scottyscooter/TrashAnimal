namespace TrashAnimal.RollPhase;

public static class RollPhaseGameplayHandlers
{
    public static IEnumerable<IGameplayHandler> CreateDefault() =>
    [
        new ShinyPlayHandler(),
        new FeeshPlayHandler(),
        new NannersBustRecoveryHandler(),
        new BlammoBustRecoveryHandler()
    ];
}
