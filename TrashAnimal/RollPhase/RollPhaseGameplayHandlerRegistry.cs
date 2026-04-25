namespace TrashAnimal.RollPhase;

public sealed class RollPhaseGameplayHandlerRegistry
{
    private readonly IGameplayHandler[] _handlers;
    private readonly Dictionary<GameAction, IGameplayHandler> _byAction;

    public RollPhaseGameplayHandlerRegistry(IEnumerable<IGameplayHandler> handlers)
    {
        _handlers = handlers.ToArray();
        _byAction = _handlers.ToDictionary(h => h.Action);
    }

    public IEnumerable<IGameplayHandler> All => _handlers;

    public bool TryGetHandler(GameAction action, out IGameplayHandler? handler) =>
        _byAction.TryGetValue(action, out handler);
}
