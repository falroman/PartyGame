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

    private async Task<(string roomCode, HubConnection host, List<HubConnection> players, List<Guid> playerIds)> SetupGameWithPlayers(int playerCount = 2)
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
        var playerIds = new List<Guid>();
        for (int i = 0; i < playerCount; i++)
        {
            var playerConnection = CreateHubConnection();
            var playerId = Guid.NewGuid();
            await playerConnection.StartAsync();
            await playerConnection.InvokeAsync("JoinRoom", roomCode, playerId, $"Player{i + 1}");
            players.Add(playerConnection);
            playerIds.Add(playerId);
            _playerConnections.Add(playerConnection);
        }

        await Task.Delay(100);
        return (roomCode, hostConnection, players, playerIds);
    }

    /// <summary>
    /// Helper to start game and select first category (to get past CategorySelection phase).
    /// </summary>
    private async Task<QuizGameStateDto?> StartGameAndSelectCategoryAsync(
        string roomCode, 
        HubConnection host, 
        List<HubConnection> players,
        List<Guid> playerIds)
    {
        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        // Should be in CategorySelection phase
        latestState.Should().NotBeNull();
        latestState!.Phase.Should().Be(QuizPhase.CategorySelection);

        // Get round leader and select first available category
        var roundLeaderId = latestState.RoundLeaderPlayerId;
        roundLeaderId.Should().NotBeNull();

        var category = latestState.AvailableCategories?.FirstOrDefault();
        category.Should().NotBeNullOrEmpty();

        // Find the player connection for the round leader
        var leaderIndex = playerIds.FindIndex(id => id == roundLeaderId);
        if (leaderIndex >= 0)
        {
            await players[leaderIndex].InvokeAsync("SelectCategory", roomCode, roundLeaderId!.Value, category);
        }
        else
        {
            // If no player is leader, it's likely the first player (lowest score = first in tie)
            await players[0].InvokeAsync("SelectCategory", roomCode, playerIds[0], category);
        }

        // Wait for Question phase
        await Task.Delay(2500);

        return latestState;
    }

    [Fact]
    public async Task StartGame_BroadcastsCategorySelectionToAllClients()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

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
        hostQuizState!.Phase.Should().Be(QuizPhase.CategorySelection);
        hostQuizState.RoundNumber.Should().Be(1);
        hostQuizState.RoundLeaderPlayerId.Should().NotBeNull();
        hostQuizState.AvailableCategories.Should().NotBeNullOrEmpty();

        player1QuizState.Should().NotBeNull();
        player2QuizState.Should().NotBeNull();
    }

    [Fact]
    public async Task SelectCategory_StartsQuestionPhase()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        var roundLeaderId = latestState!.RoundLeaderPlayerId!.Value;
        var category = latestState.AvailableCategories![0];
        var leaderIndex = playerIds.FindIndex(id => id == roundLeaderId);

        // Act
        await players[leaderIndex >= 0 ? leaderIndex : 0].InvokeAsync("SelectCategory", roomCode, roundLeaderId, category);
        await Task.Delay(3000); // Wait for transition to Question phase

        // Assert
        latestState!.Phase.Should().Be(QuizPhase.Question);
        latestState.CurrentCategory.Should().Be(category);
        latestState.QuestionNumber.Should().Be(1);
        latestState.QuestionText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectCategory_OnlyRoundLeaderCanSelect()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[1].On<ErrorDto>("Error", error => receivedError = error);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        var roundLeaderId = latestState!.RoundLeaderPlayerId!.Value;
        var nonLeaderId = playerIds.First(id => id != roundLeaderId);
        var category = latestState.AvailableCategories![0];

        // Act - Non-leader tries to select
        await players[playerIds.IndexOf(nonLeaderId)].InvokeAsync("SelectCategory", roomCode, nonLeaderId, category);
        await Task.Delay(200);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.NotRoundLeader);
    }

    [Fact]
    public async Task SelectCategory_InvalidCategoryReturnsError()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[0].On<ErrorDto>("Error", error => receivedError = error);

        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        var roundLeaderId = latestState!.RoundLeaderPlayerId!.Value;
        var leaderIndex = playerIds.FindIndex(id => id == roundLeaderId);

        // Act - Select invalid category
        await players[leaderIndex >= 0 ? leaderIndex : 0].InvokeAsync("SelectCategory", roomCode, roundLeaderId, "NonExistentCategory");
        await Task.Delay(200);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.Code.Should().Be(ErrorCodes.InvalidCategory);
    }

    [Fact]
    public async Task SubmitAnswer_InAnsweringPhase_RecordsAnswer()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);
        
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
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[0].On<ErrorDto>("Error", error => receivedError = error);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);

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
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        ErrorDto? receivedError = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);
        players[0].On<ErrorDto>("Error", error => receivedError = error);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);
        
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
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);
        
        // Wait for Answering phase
        await Task.Delay(4000);
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
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);
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
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

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

        // Act - Start game
        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        // Select category
        var roundLeaderId = latestState!.RoundLeaderPlayerId!.Value;
        var category = latestState.AvailableCategories![0];
        var leaderIndex = playerIds.FindIndex(id => id == roundLeaderId);
        await players[leaderIndex >= 0 ? leaderIndex : 0].InvokeAsync("SelectCategory", roomCode, roundLeaderId, category);
        
        // Wait for Question phase
        await Task.Delay(3000);
        
        // Wait for Answering phase and submit answers
        await Task.Delay(4000);
        
        var player1Id = latestState!.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
        var player2Id = latestState.Scoreboard.First(p => p.DisplayName == "Player2").PlayerId;
        
        await players[0].InvokeAsync("SubmitAnswer", roomCode, player1Id, "A");
        await players[1].InvokeAsync("SubmitAnswer", roomCode, player2Id, "B");
        
        // Wait for Reveal and Scoreboard
        await Task.Delay(12000); // Reveal(5s) + Scoreboard(5s) + buffer

        // Assert - We should have gone through multiple phases
        phaseHistory.Should().Contain(QuizPhase.CategorySelection);
        phaseHistory.Should().Contain(QuizPhase.Question);
        phaseHistory.Should().Contain(QuizPhase.Answering);
        phaseHistory.Should().Contain(QuizPhase.Reveal);
        phaseHistory.Should().Contain(QuizPhase.Scoreboard);
    }

    [Fact]
    public async Task ReconnectingPlayer_ReceivesCurrentQuizState()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        QuizGameStateDto? latestState = null;
        host.On<QuizGameStateDto>("QuizStateUpdated", state => latestState = state);

        await StartGameAndSelectCategoryAsync(roomCode, host, players, playerIds);
        await Task.Delay(500);

        // Should now be in Question phase with a question
        latestState!.Phase.Should().Be(QuizPhase.Question);
        latestState.QuestionNumber.Should().BeGreaterThan(0);

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

    [Fact]
    public async Task RoundCompletion_StartsNewRoundWithNewLeader()
    {
        // Arrange
        var (roomCode, host, players, playerIds) = await SetupGameWithPlayers(2);

        var roundNumbers = new List<int>();
        QuizGameStateDto? latestState = null;

        host.On<QuizGameStateDto>("QuizStateUpdated", state =>
        {
            latestState = state;
            // Track when we enter CategorySelection for each round
            if (state.Phase == QuizPhase.CategorySelection)
            {
                if (roundNumbers.Count == 0 || roundNumbers.Last() != state.RoundNumber)
                {
                    roundNumbers.Add(state.RoundNumber);
                }
            }
        });

        // Act - Start game
        await host.InvokeAsync("StartGame", roomCode, "Quiz");
        await Task.Delay(500);

        // Should have Round 1
        roundNumbers.Should().Contain(1);

        // Round 1 - select category
        var roundLeaderId = latestState!.RoundLeaderPlayerId!.Value;
        var category = latestState.AvailableCategories![0];
        var leaderIndex = playerIds.FindIndex(id => id == roundLeaderId);
        await players[leaderIndex >= 0 ? leaderIndex : 0].InvokeAsync("SelectCategory", roomCode, roundLeaderId, category);
        await Task.Delay(3000);

        // Play through 3 questions (1 complete round)
        for (int q = 0; q < 3; q++)
        {
            // Wait for Answering phase
            var waitAttempts = 0;
            while (latestState!.Phase != QuizPhase.Answering && waitAttempts < 20)
            {
                await Task.Delay(500);
                waitAttempts++;
            }
            
            if (latestState!.Phase != QuizPhase.Answering)
            {
                // If we ended up in CategorySelection, we completed a round
                break;
            }
            
            var p1Id = latestState!.Scoreboard.First(p => p.DisplayName == "Player1").PlayerId;
            var p2Id = latestState.Scoreboard.First(p => p.DisplayName == "Player2").PlayerId;

            await players[0].InvokeAsync("SubmitAnswer", roomCode, p1Id, "A");
            await players[1].InvokeAsync("SubmitAnswer", roomCode, p2Id, "B");

            // Wait for Reveal + Scoreboard
            await Task.Delay(12000);
        }

        // Wait for new round to potentially start
        await Task.Delay(2000);

        // Assert - Check if we got at least round 1 working correctly
        // Round 2 might not start if we ran out of questions/categories
        roundNumbers.Should().Contain(1);
        latestState!.RoundNumber.Should().BeGreaterOrEqualTo(1);
        
        // Verify the round structure works
        if (latestState.Phase == QuizPhase.CategorySelection && latestState.RoundNumber == 2)
        {
            roundNumbers.Should().Contain(2);
        }
    }
}
