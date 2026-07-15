namespace TrashAnimal.Api.Startup.Options;

public sealed class CorsOptions
{
    /// <summary>
    /// Origins allowed to call the API and negotiate the SignalR hub (e.g. the Vite dev server).
    /// Must contain at least one entry.
    /// </summary>
    public IEnumerable<string> AllowedOrigins { get; set; } = [];
}
