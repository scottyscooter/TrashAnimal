using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Verifies the "Frontend" CORS policy registered in <c>Program.cs</c>: the origin configured via
/// <c>CorsOptions:AllowedOrigins</c> (<c>http://localhost:5173</c> in <c>appsettings.json</c>) is
/// granted access, and unlisted origins are not.
/// </summary>
public sealed class CorsPolicyTests : IClassFixture<TrashApiTestFactory>
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string DisallowedOrigin = "http://evil.example.com";

    private readonly HttpClient _http;

    public CorsPolicyTests(TrashApiTestFactory factory) => _http = factory.CreateClient();

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsAccessControlHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/games");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _http.SendAsync(request);

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_OmitsAccessControlAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/games");
        request.Headers.Add("Origin", DisallowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _http.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ActualRequest_FromAllowedOrigin_ReturnsAccessControlAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/games/00000000-0000-0000-0000-000000000000/view?playerSeat=0");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _http.SendAsync(request);

        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task ActualRequest_FromDisallowedOrigin_OmitsAccessControlAllowOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/games/00000000-0000-0000-0000-000000000000/view?playerSeat=0");
        request.Headers.Add("Origin", DisallowedOrigin);

        var response = await _http.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
