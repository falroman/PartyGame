using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace PartyGame.Tests.Integration;

/// <summary>
/// Integration tests for the Server API to verify basic endpoints work.
/// </summary>
public class ServerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ServerApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsJsonWithStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("healthy");
        content.Should().Contain("timestamp");
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsOk_InDevelopment()
    {
        // Arrange - Create client with development environment
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
