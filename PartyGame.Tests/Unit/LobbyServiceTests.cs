using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Server.DTOs;
using PartyGame.Server.Hubs;
using PartyGame.Server.Services;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for LobbyService, focusing on room locking and host-only actions.
/// </summary>
public class LobbyServiceTests
{
    private readonly IRoomStore _roomStore;
    private readonly IConnectionIndex _connectionIndex;
    private readonly IClock _clock;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<LobbyService> _logger;
    private readonly LobbyService _sut;

    public LobbyServiceTests()
    {
        _roomStore = Substitute.For<IRoomStore>();
        _connectionIndex = Substitute.For<IConnectionIndex>();
        _clock = Substitute.For<IClock>();
        _hubContext = Substitute.For<IHubContext<GameHub>>();
        _logger = Substitute.For<ILogger<LobbyService>>();

        // Setup default clock
        _clock.UtcNow.Returns(DateTime.UtcNow);

        // Setup hub context to return a mock clients
        var mockClients = Substitute.For<IHubClients>();
        var mockClientProxy = Substitute.For<IClientProxy>();
        mockClients.Group(Arg.Any<string>()).Returns(mockClientProxy);
        _hubContext.Clients.Returns(mockClients);

        _sut = new LobbyService(_roomStore, _connectionIndex, _clock, _hubContext, _logger);
    }

    #region SetRoomLockedAsync Tests

    [Fact]
    public async Task SetRoomLockedAsync_WithValidHostAndRoom_SetsLockedState()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            IsLocked = false
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.SetRoomLockedAsync(roomCode, hostConnectionId, true);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        room.IsLocked.Should().BeTrue();
        _roomStore.Received(1).Update(room);
    }

    [Fact]
    public async Task SetRoomLockedAsync_WithValidHostAndRoom_CanUnlock()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            IsLocked = true
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.SetRoomLockedAsync(roomCode, hostConnectionId, false);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        room.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task SetRoomLockedAsync_WithNonExistentRoom_ReturnsRoomNotFoundError()
    {
        // Arrange
        var roomCode = "XXXX";
        var connectionId = "some-connection";

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = null;
                return false;
            });

        // Act
        var (success, error) = await _sut.SetRoomLockedAsync(roomCode, connectionId, true);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.RoomNotFound);
    }

    [Fact]
    public async Task SetRoomLockedAsync_WithNonHostConnection_ReturnsNotHostError()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var otherConnectionId = "other-connection-456";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            IsLocked = false
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.SetRoomLockedAsync(roomCode, otherConnectionId, true);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.NotHost);
        room.IsLocked.Should().BeFalse(); // Should not have changed
    }

    [Fact]
    public async Task SetRoomLockedAsync_WithNoHostRegistered_ReturnsNotHostError()
    {
        // Arrange
        var roomCode = "TEST";
        var connectionId = "some-connection";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = null, // No host registered
            IsLocked = false
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.SetRoomLockedAsync(roomCode, connectionId, true);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.NotHost);
    }

    #endregion

    #region AddBotPlayersAsync Tests

    [Fact]
    public async Task AddBotPlayersAsync_AddsBotsWithUniqueIdsAndNames()
    {
        // Arrange
        var roomCode = "TEST";
        var room = new Room
        {
            Code = roomCode,
            MaxPlayers = 10,
            Players = new Dictionary<Guid, Player>()
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.AddBotPlayersAsync(roomCode, 3);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        room.Players.Count.Should().Be(3);
        room.Players.Values.Should().OnlyContain(p => p.IsBot);
        room.Players.Values.Select(p => p.PlayerId).Distinct().Count().Should().Be(3);
        room.Players.Values.Select(p => p.DisplayName).Distinct().Count().Should().Be(3);
        room.Players.Values.Should().OnlyContain(p => p.BotSkill is >= 0 and <= 100);
        _hubContext.Clients.Received(1).Group($"room:{roomCode}");
    }

    #endregion

    #region JoinRoomAsync - Locked Room Tests

    [Fact]
    public async Task JoinRoomAsync_WhenRoomIsLocked_ReturnsRoomLockedError()
    {
        // Arrange
        var roomCode = "TEST";
        var playerId = Guid.NewGuid();
        var connectionId = "player-connection";
        var room = new Room
        {
            Code = roomCode,
            IsLocked = true,
            Players = new Dictionary<Guid, Player>()
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.JoinRoomAsync(roomCode, playerId, "TestPlayer", connectionId);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.RoomLocked);
    }

    [Fact]
    public async Task JoinRoomAsync_WhenRoomIsLockedButPlayerIsReconnecting_AllowsReconnect()
    {
        // Arrange
        var roomCode = "TEST";
        var playerId = Guid.NewGuid();
        var connectionId = "player-connection";
        var existingPlayer = new Player
        {
            PlayerId = playerId,
            DisplayName = "ExistingPlayer",
            IsConnected = false,
            ConnectionId = null
        };
        var room = new Room
        {
            Code = roomCode,
            IsLocked = true,
            Players = new Dictionary<Guid, Player> { { playerId, existingPlayer } }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.JoinRoomAsync(roomCode, playerId, "ExistingPlayer", connectionId);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        existingPlayer.IsConnected.Should().BeTrue();
        existingPlayer.ConnectionId.Should().Be(connectionId);
    }

    [Fact]
    public async Task JoinRoomAsync_WhenRoomIsNotLocked_AllowsNewPlayer()
    {
        // Arrange
        var roomCode = "TEST";
        var playerId = Guid.NewGuid();
        var connectionId = "player-connection";
        var room = new Room
        {
            Code = roomCode,
            IsLocked = false,
            Players = new Dictionary<Guid, Player>()
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.JoinRoomAsync(roomCode, playerId, "NewPlayer", connectionId);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        room.Players.Should().ContainKey(playerId);
    }

    #endregion

    #region IsHostOfRoom Tests

    [Fact]
    public void IsHostOfRoom_WithMatchingConnectionId_ReturnsTrue()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var result = _sut.IsHostOfRoom(roomCode, hostConnectionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHostOfRoom_WithDifferentConnectionId_ReturnsFalse()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var otherConnectionId = "other-connection-456";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var result = _sut.IsHostOfRoom(roomCode, otherConnectionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHostOfRoom_WithNonExistentRoom_ReturnsFalse()
    {
        // Arrange
        var roomCode = "XXXX";
        var connectionId = "some-connection";

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(false);

        // Act
        var result = _sut.IsHostOfRoom(roomCode, connectionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHostOfRoom_WithNoHostRegistered_ReturnsFalse()
    {
        // Arrange
        var roomCode = "TEST";
        var connectionId = "some-connection";
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = null
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var result = _sut.IsHostOfRoom(roomCode, connectionId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region StartGameAsync Tests

    [Fact]
    public async Task StartGameAsync_WithValidHostAndEnoughPlayers_StartsGame()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var player1 = new Player { PlayerId = Guid.NewGuid(), DisplayName = "Player1", IsConnected = true };
        var player2 = new Player { PlayerId = Guid.NewGuid(), DisplayName = "Player2", IsConnected = true };
        
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            Status = RoomStatus.Lobby,
            IsLocked = false,
            Players = new Dictionary<Guid, Player>
            {
                { player1.PlayerId, player1 },
                { player2.PlayerId, player2 }
            }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        var gameStartTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow.Returns(gameStartTime);

        // Act
        var (success, error) = await _sut.StartGameAsync(roomCode, hostConnectionId, GameType.Quiz);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        room.Status.Should().Be(RoomStatus.InGame);
        room.IsLocked.Should().BeTrue();
        room.CurrentGame.Should().NotBeNull();
        room.CurrentGame!.GameType.Should().Be(GameType.Quiz);
        room.CurrentGame.Phase.Should().Be("Starting");
        room.CurrentGame.StartedUtc.Should().Be(gameStartTime);
        _roomStore.Received(1).Update(room);
    }

    [Fact]
    public async Task StartGameAsync_WithNonHost_ReturnsNotHostError()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        var playerConnectionId = "player-connection-456";
        
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            Status = RoomStatus.Lobby,
            Players = new Dictionary<Guid, Player>
            {
                { Guid.NewGuid(), new Player { DisplayName = "Player1" } },
                { Guid.NewGuid(), new Player { DisplayName = "Player2" } }
            }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.StartGameAsync(roomCode, playerConnectionId, GameType.Quiz);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.NotHost);
        room.Status.Should().Be(RoomStatus.Lobby); // Should not change
    }

    [Fact]
    public async Task StartGameAsync_WithLessThan2Players_ReturnsNotEnoughPlayersError()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            Status = RoomStatus.Lobby,
            Players = new Dictionary<Guid, Player>
            {
                { Guid.NewGuid(), new Player { DisplayName = "Player1" } }
            }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.StartGameAsync(roomCode, hostConnectionId, GameType.Quiz);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.NotEnoughPlayers);
    }

    [Fact]
    public async Task StartGameAsync_WithNonExistentRoom_ReturnsRoomNotFoundError()
    {
        // Arrange
        _roomStore.TryGetRoom("XXXX", out Arg.Any<Room?>()).Returns(false);

        // Act
        var (success, error) = await _sut.StartGameAsync("XXXX", "connection-id", GameType.Quiz);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.RoomNotFound);
    }

    [Fact]
    public async Task StartGameAsync_WhenAlreadyInGame_ReturnsInvalidStateError()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            Status = RoomStatus.InGame, // Already in game
            Players = new Dictionary<Guid, Player>
            {
                { Guid.NewGuid(), new Player { DisplayName = "Player1" } },
                { Guid.NewGuid(), new Player { DisplayName = "Player2" } }
            }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var (success, error) = await _sut.StartGameAsync(roomCode, hostConnectionId, GameType.Quiz);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task StartGameAsync_BroadcastsLobbyUpdatedAndGameStarted()
    {
        // Arrange
        var roomCode = "TEST";
        var hostConnectionId = "host-connection-123";
        
        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = hostConnectionId,
            Status = RoomStatus.Lobby,
            Players = new Dictionary<Guid, Player>
            {
                { Guid.NewGuid(), new Player { DisplayName = "Player1", IsConnected = true } },
                { Guid.NewGuid(), new Player { DisplayName = "Player2", IsConnected = true } }
            }
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        await _sut.StartGameAsync(roomCode, hostConnectionId, GameType.Quiz);

        // Assert - Should broadcast both events
        var mockClients = _hubContext.Clients;
        mockClients.Received(2).Group($"room:{roomCode}"); // Called twice: LobbyUpdated and GameStarted
    }

    #endregion
}
