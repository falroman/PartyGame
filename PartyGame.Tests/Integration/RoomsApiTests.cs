using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PartyGame.Core.Enums;
using PartyGame.Server.DTOs;

namespace PartyGame.Tests.Integration;

/// <summary>
/// Integration tests for the Rooms API endpoints.
/// </summary>
public class RoomsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public RoomsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task CreateRoom_ReturnsCreated()
    {
        // Act
        var response = await _client.PostAsync("/api/rooms", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateRoom_ReturnsValidRoomCode()
    {
        // Act
        var response = await _client.PostAsync("/api/rooms", null);
        var content = await response.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);

        // Assert
        content.Should().NotBeNull();
        content!.RoomCode.Should().HaveLength(4);
        content.RoomCode.Should().MatchRegex("^[A-Z0-9]{4}$");
    }

    [Fact]
    public async Task CreateRoom_ReturnsJoinUrl()
    {
        // Act
        var response = await _client.PostAsync("/api/rooms", null);
        var content = await response.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);

        // Assert
        content.Should().NotBeNull();
        content!.JoinUrl.Should().StartWith("/join/");
        content.JoinUrl.Should().Contain(content.RoomCode);
    }

    [Fact]
    public async Task CreateRoom_ReturnsLocationHeader()
    {
        // Act
        var response = await _client.PostAsync("/api/rooms", null);

        // Assert
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().ToLowerInvariant().Should().Contain("/api/rooms/");
    }

    [Fact]
    public async Task GetRoom_WithValidCode_ReturnsOk()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/rooms", null);
        var createContent = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);
        var roomCode = createContent!.RoomCode;

        // Act
        var response = await _client.GetAsync($"/api/rooms/{roomCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoom_WithValidCode_ReturnsRoomState()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/rooms", null);
        var createContent = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);
        var roomCode = createContent!.RoomCode;

        // Act
        var response = await _client.GetAsync($"/api/rooms/{roomCode}");
        var content = await response.Content.ReadFromJsonAsync<RoomStateDto>(_jsonOptions);

        // Assert
        content.Should().NotBeNull();
        content!.RoomCode.Should().Be(roomCode);
        content.Status.Should().Be(RoomStatus.Lobby);
        content.IsLocked.Should().BeFalse();
        content.Players.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoom_WithInvalidCode_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/XXXX");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoom_WithInvalidCode_ReturnsErrorDto()
    {
        // Act
        var response = await _client.GetAsync("/api/rooms/XXXX");
        var content = await response.Content.ReadFromJsonAsync<ErrorDto>(_jsonOptions);

        // Assert
        content.Should().NotBeNull();
        content!.Code.Should().Be("ROOM_NOT_FOUND");
        content.Message.Should().Contain("XXXX");
    }

    [Fact]
    public async Task GetRoom_IsCaseInsensitive()
    {
        // Arrange
        var createResponse = await _client.PostAsync("/api/rooms", null);
        var createContent = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);
        var roomCode = createContent!.RoomCode;

        // Act
        var responseLower = await _client.GetAsync($"/api/rooms/{roomCode.ToLowerInvariant()}");

        // Assert
        responseLower.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateMultipleRooms_ReturnsUniqueRoomCodes()
    {
        // Arrange
        var roomCodes = new HashSet<string>();
        const int numberOfRooms = 10;

        // Act
        for (int i = 0; i < numberOfRooms; i++)
        {
            var response = await _client.PostAsync("/api/rooms", null);
            var content = await response.Content.ReadFromJsonAsync<CreateRoomResponseDto>(_jsonOptions);
            roomCodes.Add(content!.RoomCode);
        }

        // Assert
        roomCodes.Should().HaveCount(numberOfRooms);
    }
}
