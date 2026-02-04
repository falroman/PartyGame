using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using PartyGame.Core.Enums;
using PartyGame.Server.DTOs;

namespace PartyGame.Tests.Integration;

/// <summary>
/// Integration tests for Quiz gameplay through SignalR.
/// </summary>
public class QuizGameplayTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;
    private HubConnection? _hostConnection;
    private readonly List<HubConnection> _playerConnections = new();

    public QuizGameplayTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _httpClient = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _playerConnections)
        {
            await connection.DisposeAsync();
        }
        if (_hostConnection != null)
        {
            await _hostConnection.DisposeAsync();
        }
    }

    private HubConnection CreateHubConnection()
    {
        var hubUrl = new Uri(_httpClient.BaseAddress!, "/hub/game");
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    private async Task<(string roomCode, HubConnection host, List<HubConnection> players)> SetupGameWithPlayers(int playerCount = 2)
    {
        // Create room
        var createResponse = await _httpClient.PostAsync("/api/rooms", null);
        var roomData = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponseDto>();
        var roomCode = roomData!.RoomCode;

        // Set up host
        var hostConnection = CreateHubConnection();
        await hostConnection.StartAsync();
        await hostConnection.InvokeAsync("RegisterHost", roomCode);
        _hostConnection = hostConnection;

        // Set up players
        var players = new List<HubConnection>();
        for (int i = 0; i < playerCount; i++)
        {
            var playerConnection = CreateHubConnection();
            await playerConnection.StartAsync();
            await playerConnection.InvokeAsync("JoinRoom", roomCode, Guid.NewGuid(), $"Player{i + 1}");
            players.Add(playerConnection);
            _playerConnections.Add(playerConnection);
        }

        await Task.Delay(100);
        return (roomCode, hostConnection, players);
    }

    [Fact]
    public async Task StartGame_BroadcastsQuizStateToAllClients()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? hostQuizState = null;
        QuizGameStateDto? player1QuizState = null;
        QuizGameStateDto? player2QuizState = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => hostQuizState = state);
        players[0].On<QuizGameStateDto>("QuizStateUpdated", state => player1QuizState = state);
        players[1].On<QuizGameStateDto>("QuizStateUpdated", state => player2QuizState = state);

        // Act
        await host.InvokeAsync("StartGame", roomCode, "Quiz");

        // Assert - Wait for initial state broadcast
        await Task.Delay(500);

        hostQuizState.Should().NotBeNull();
        hostQuizState!.QuestionNumber.Should().Be(1);
        hostQuizState.QuestionText.Should().NotBeEmpty();
        hostQuizState.Options.Should().HaveCount(4);
        hostQuizState.CorrectOptionKey.Should().BeNull(); // Hidden during Question phase
        hostQuizState.Phase.Should().Be(QuizPhase.Question);

        player1QuizState.Should().NotBeNull();
        player2QuizState.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAnswer_InAnsweringPhase_RecordsAnswer()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        // Wait for Answering phase (3 seconds after Question phase)
        await Task.Delay(3500);

        // Check we're in Answering phase
        latestState.Should().NotBeNull();
        latestState!.Phase.Should().Be(QuizPhase.Answering);

        // Get player ID from scoreboard
        var player1Id = latestState.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;

        // Act
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await Task.Delay(200);

        // Assert
        latestState!.AnswerStatuses.Should().Contain(s => s.PlayerId == player1Id && s.HasAnswered);
    }

    [Fact]
    public async Task SubmitAnswer_InWrongPhase_ReturnsError()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[0].On<ErrorDto>("Error", error => receivedError = error);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(200);

        // Still in Question phase (before Answering)
        latestState!.Phase.Should().Be(QuizPhase.Question);

        var player1Id = latestState.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;

        // Act - Try to submit during Question phase
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await Task.Delay(100);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task SubmitAnswer_WithInvalidOption_ReturnsError()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[0].On<ErrorDto>("Error", error => receivedError = error);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        
        // Wait for Answering phase
        await Task.Delay(4000);
        latestState!.Phase.Should().Be(QuizPhase.Answering);

        var player1Id = latestState.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;

        // Act - Submit invalid option
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "Z");
        await Task.Delay(100);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task RevealPhase_ShowsCorrectAnswer()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        
        // Wait through Question -> Answering -> Reveal phases
        // Question: 3s, Answering: 15s, but we can speed this up by having all players answer
        await Task.Delay(4000); // Question phase ends, Answering starts

        latestState!.Phase.Should().Be(QuizPhase.Answering);

        // Both players answer to trigger early advance
        var player1Id = latestState.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
        var player2Id = latestState.Scoreboard.First(p => p.DisplayName == "Player2").PlayerId;

        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await players[1].InvokeAsync("SubmitAnswer", roomCode, player2Id, "B");
        
        // Should advance to Reveal quickly since all answered
        await Task.Delay(500);

        // Assert
        latestState!.Phase.Should().Be(QuizPhase.Reveal);
        latestState.CorrectOptionKey.Should().NotBeNull();
        latestState.Explanation.Should().NotBeNull();
        latestState.Scoreboard.Should().AllSatisfy(p => p.AnsweredCorrectly.Should().NotBeNull());
    }

    [Fact]
    public async Task AllPlayersAnswer_AdvancesToRevealEarly()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(4000); // Wait for Answering phase

        latestState!.Phase.Should().Be(QuizPhase.Answering);

        var player1Id = latestState.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
        var player2Id = latestState.Scoreboard.First(p => p.DisplayName == "Player2").PlayerId;

        // Act - Both players answer
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await players[1].InvokeAsync("SubmitAnswer", roomCode, player2Id, "B");

        // Should advance early without waiting full 15 seconds
        await Task.Delay(500);

        // Assert
        latestState!.Phase.Should().Be(QuizPhase.Reveal);
    }

    [Fact]
    public async Task GameFlow_CompletesFullRound()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        var phaseHistory = new List<QuizPhase>();
        QuizGameStateDto? latestState = null;
        
        host.On<QuizGameStateDto>("QuizStateUpdated", state => 
        {
            latestState = state;
            if (phaseHistory.Count == 0 || phaseHistory.Last() != state.Phase)
            {
                phaseHistory.Add(state.Phase);
            }
        });

        // Act - Start game and let it run
        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        
        // Wait for Answering phase and submit answers
        await Task.Delay(4000);
        
        var player1Id = latestState!.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
        var player2Id = latestState.Scoreboard.First(p => p.DisplayName == "Player2").PlayerId;
        
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await players[1].InvokeAsync("SubmitAnswer", roomCode, player2Id, "B");
        
        // Wait for Reveal and Scoreboard
        await Task.Delay(12000); // Reveal(5s) + Scoreboard(5s) + buffer

        // Assert - We should have gone through multiple phases
        phaseHistory.Should().Contain(QuizPhase.Question);
        phaseHistory.Should().Contain(QuizPhase.Answering);
        phaseHistory.Should().Contain(QuizPhase.Reveal);
        phaseHistory.Should().Contain(QuizPhase.Scoreboard);
    }

    [Fact]
    public async Task ReconnectingPlayer_ReceivesCurrentQuizState()
    {
        // Arrange
        var (roomCode, host, players) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        // Disconnect player 1
        var player1Id = latestState!.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
        await players[0].DisposeAsync();
        _playerConnections.Remove(players[0]);
        await Task.Delay(100);

        // Act - Reconnect with same player ID
        var reconnectedPlayer = CreateHubConnection();
        QuizGameStateDto? reconnectedState = null;
        reconnectedPlayer.On<QuizGameStateDto>("QuizStateUpdated", state => reconnectedState = state);

        await reconnectedPlayer.StartAsync();
        await reconnectedPlayer.InvokeAsync("JoinRoom", roomCode, player1Id, "Player1");
        _playerConnections.Add(reconnectedPlayer);
        
        await Task.Delay(300);

        // Assert - Reconnected player should receive current quiz state
        reconnectedState.Should().NotBeNull();
        reconnectedState!.QuestionNumber.Should().BeGreaterThan(0);
    }
}
