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
/// Unit tests for room cleanup functionality in LobbyService.
/// Tests use a fake clock to control time advancement.
/// </summary>
public class RoomCleanupTests
{
    private readonly IRoomStore _roomStore;
    private readonly IConnectionIndex _connectionIndex;
    private readonly TestClock _clock;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<LobbyService> _logger;
    private readonly LobbyService _sut;
    private readonly IClientProxy _mockClientProxy;

    public RoomCleanupTests()
    {
        _roomStore = Substitute.For<IRoomStore>();
        _connectionIndex = Substitute.For<IConnectionIndex>();
        _clock = new TestClock();
        _hubContext = Substitute.For<IHubContext<GameHub>>();
        _logger = Substitute.For<ILogger<LobbyService>>();

        // Setup hub context
        var mockClients = Substitute.For<IHubClients>();
        _mockClientProxy = Substitute.For<IClientProxy>();
        mockClients.Group(Arg.Any<string>()).Returns(_mockClientProxy);
        _hubContext.Clients.Returns(mockClients);

        _sut = new LobbyService(_roomStore, _connectionIndex, _clock, _hubContext, _logger);
    }

    #region RemoveDisconnectedPlayersAsync Tests

    [Fact]
    public async Task RemoveDisconnectedPlayersAsync_RemovesPlayersExceedingGracePeriod()
    {
        // Arrange
        var roomCode = "TEST";
        var gracePeriod = TimeSpan.FromSeconds(120);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        var disconnectedPlayer = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "DisconnectedPlayer",
            IsConnected = false,
            LastSeenUtc = baseTime.AddSeconds(-150) // 150 seconds ago (> grace period)
        };
        
        var connectedPlayer = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "ConnectedPlayer",
            IsConnected = true,
            LastSeenUtc = baseTime
        };

        var room = new Room
        {
            Code = roomCode,
            Players = new Dictionary<Guid, Player>
            {
                { disconnectedPlayer.PlayerId, disconnectedPlayer },
                { connectedPlayer.PlayerId, connectedPlayer }
            }
        };

        _clock.UtcNow = baseTime;
        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var removedCount = await _sut.RemoveDisconnectedPlayersAsync(roomCode, gracePeriod);

        // Assert
        removedCount.Should().Be(1);
        room.Players.Should().HaveCount(1);
        room.Players.Should().ContainKey(connectedPlayer.PlayerId);
        room.Players.Should().NotContainKey(disconnectedPlayer.PlayerId);
        _roomStore.Received(1).Update(room);
    }

    [Fact]
    public async Task RemoveDisconnectedPlayersAsync_KeepsPlayersWithinGracePeriod()
    {
        // Arrange
        var roomCode = "TEST";
        var gracePeriod = TimeSpan.FromSeconds(120);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        var recentlyDisconnectedPlayer = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "RecentlyDisconnected",
            IsConnected = false,
            LastSeenUtc = baseTime.AddSeconds(-60) // 60 seconds ago (< grace period)
        };

        var room = new Room
        {
            Code = roomCode,
            Players = new Dictionary<Guid, Player>
            {
                { recentlyDisconnectedPlayer.PlayerId, recentlyDisconnectedPlayer }
            }
        };

        _clock.UtcNow = baseTime;
        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var removedCount = await _sut.RemoveDisconnectedPlayersAsync(roomCode, gracePeriod);

        // Assert
        removedCount.Should().Be(0);
        room.Players.Should().HaveCount(1);
        _roomStore.DidNotReceive().Update(Arg.Any<Room>());
    }

    [Fact]
    public async Task RemoveDisconnectedPlayersAsync_BroadcastsLobbyUpdatedWhenPlayersRemoved()
    {
        // Arrange
        var roomCode = "TEST";
        var gracePeriod = TimeSpan.FromSeconds(120);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        var disconnectedPlayer = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "DisconnectedPlayer",
            IsConnected = false,
            LastSeenUtc = baseTime.AddSeconds(-150)
        };

        var room = new Room
        {
            Code = roomCode,
            Players = new Dictionary<Guid, Player>
            {
                { disconnectedPlayer.PlayerId, disconnectedPlayer }
            }
        };

        _clock.UtcNow = baseTime;
        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        await _sut.RemoveDisconnectedPlayersAsync(roomCode, gracePeriod);

        // Assert
        await _mockClientProxy.Received(1).SendCoreAsync(
            "LobbyUpdated",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveDisconnectedPlayersAsync_ReturnsZeroForNonExistentRoom()
    {
        // Arrange
        _roomStore.TryGetRoom("XXXX", out Arg.Any<Room?>()).Returns(false);

        // Act
        var removedCount = await _sut.RemoveDisconnectedPlayersAsync("XXXX", TimeSpan.FromSeconds(120));

        // Assert
        removedCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveDisconnectedPlayersAsync_HandlesMultipleDisconnectedPlayers()
    {
        // Arrange
        var roomCode = "TEST";
        var gracePeriod = TimeSpan.FromSeconds(60);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        var players = new Dictionary<Guid, Player>();
        for (int i = 0; i < 5; i++)
        {
            var player = new Player
            {
                PlayerId = Guid.NewGuid(),
                DisplayName = $"Player{i}",
                IsConnected = false,
                LastSeenUtc = baseTime.AddSeconds(-120) // All disconnected > grace
            };
            players[player.PlayerId] = player;
        }

        var room = new Room { Code = roomCode, Players = players };

        _clock.UtcNow = baseTime;
        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        var removedCount = await _sut.RemoveDisconnectedPlayersAsync(roomCode, gracePeriod);

        // Assert
        removedCount.Should().Be(5);
        room.Players.Should().BeEmpty();
    }

    #endregion

    #region GetHostlessRoomsForCleanup Tests

    [Fact]
    public void GetHostlessRoomsForCleanup_ReturnsRoomsWithDisconnectedHostExceedingTtl()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(10);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = baseTime;

        var hostlessRoom = new Room
        {
            Code = "HOST",
            HostConnectionId = null,
            HostDisconnectedAtUtc = baseTime.AddMinutes(-15) // 15 minutes ago (> TTL)
        };

        var activeRoom = new Room
        {
            Code = "ACTV",
            HostConnectionId = "active-host-connection"
        };

        _roomStore.GetAll().Returns(new[] { hostlessRoom, activeRoom });

        // Act
        var roomsToRemove = _sut.GetHostlessRoomsForCleanup(ttl);

        // Assert
        roomsToRemove.Should().HaveCount(1);
        roomsToRemove.Should().Contain("HOST");
        roomsToRemove.Should().NotContain("ACTV");
    }

    [Fact]
    public void GetHostlessRoomsForCleanup_KeepsRecentlyDisconnectedHostRooms()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(10);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = baseTime;

        var recentlyHostlessRoom = new Room
        {
            Code = "RCNT",
            HostConnectionId = null,
            HostDisconnectedAtUtc = baseTime.AddMinutes(-5) // 5 minutes ago (< TTL)
        };

        _roomStore.GetAll().Returns(new[] { recentlyHostlessRoom });

        // Act
        var roomsToRemove = _sut.GetHostlessRoomsForCleanup(ttl);

        // Assert
        roomsToRemove.Should().BeEmpty();
    }

    [Fact]
    public void GetHostlessRoomsForCleanup_RemovesRoomsWhereHostNeverRegistered()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(10);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = baseTime;

        var neverHostedRoom = new Room
        {
            Code = "NEVR",
            HostConnectionId = null,
            HostDisconnectedAtUtc = null, // Host never registered
            CreatedUtc = baseTime.AddMinutes(-15) // Created 15 minutes ago (> TTL)
        };

        _roomStore.GetAll().Returns(new[] { neverHostedRoom });

        // Act
        var roomsToRemove = _sut.GetHostlessRoomsForCleanup(ttl);

        // Assert
        roomsToRemove.Should().HaveCount(1);
        roomsToRemove.Should().Contain("NEVR");
    }

    [Fact]
    public void GetHostlessRoomsForCleanup_KeepsRecentlyCreatedRoomsWithoutHost()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(10);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = baseTime;

        var newRoom = new Room
        {
            Code = "NEWW",
            HostConnectionId = null,
            HostDisconnectedAtUtc = null,
            CreatedUtc = baseTime.AddMinutes(-2) // Created 2 minutes ago (< TTL)
        };

        _roomStore.GetAll().Returns(new[] { newRoom });

        // Act
        var roomsToRemove = _sut.GetHostlessRoomsForCleanup(ttl);

        // Assert
        roomsToRemove.Should().BeEmpty();
    }

    [Fact]
    public void GetHostlessRoomsForCleanup_ReturnsMultipleHostlessRooms()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(10);
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = baseTime;

        var rooms = new[]
        {
            new Room
            {
                Code = "OLD1",
                HostConnectionId = null,
                HostDisconnectedAtUtc = baseTime.AddMinutes(-20)
            },
            new Room
            {
                Code = "OLD2",
                HostConnectionId = null,
                HostDisconnectedAtUtc = baseTime.AddMinutes(-15)
            },
            new Room
            {
                Code = "ACTV",
                HostConnectionId = "host-conn"
            }
        };

        _roomStore.GetAll().Returns(rooms);

        // Act
        var roomsToRemove = _sut.GetHostlessRoomsForCleanup(ttl);

        // Assert
        roomsToRemove.Should().HaveCount(2);
        roomsToRemove.Should().Contain("OLD1");
        roomsToRemove.Should().Contain("OLD2");
    }

    #endregion

    #region RemoveRoom Tests

    [Fact]
    public void RemoveRoom_CallsRoomStoreRemove()
    {
        // Arrange
        var roomCode = "TEST";

        // Act
        _sut.RemoveRoom(roomCode);

        // Assert
        _roomStore.Received(1).Remove(roomCode.ToUpperInvariant());
    }

    [Fact]
    public void RemoveRoom_NormalizesRoomCode()
    {
        // Act
        _sut.RemoveRoom("test");

        // Assert
        _roomStore.Received(1).Remove("TEST");
    }

    #endregion

    #region HandleDisconnectAsync - Host Disconnect Timestamp Tests

    [Fact]
    public async Task HandleDisconnectAsync_SetsHostDisconnectedAtUtc_WhenHostDisconnects()
    {
        // Arrange
        var roomCode = "TEST";
        var connectionId = "host-connection";
        var disconnectTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = disconnectTime;

        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = connectionId,
            HostDisconnectedAtUtc = null
        };

        var binding = new ConnectionBinding
        {
            ConnectionId = connectionId,
            RoomCode = roomCode,
            Role = ClientRole.Host,
            PlayerId = null
        };

        _connectionIndex.TryGet(connectionId, out Arg.Any<ConnectionBinding?>())
            .Returns(x =>
            {
                x[1] = binding;
                return true;
            });

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        await _sut.HandleDisconnectAsync(connectionId);

        // Assert
        room.HostConnectionId.Should().BeNull();
        room.HostDisconnectedAtUtc.Should().Be(disconnectTime);
        _roomStore.Received(1).Update(room);
    }

    #endregion

    #region RegisterHostAsync - Clears Disconnect Timestamp Tests

    [Fact]
    public async Task RegisterHostAsync_ClearsHostDisconnectedAtUtc_WhenHostReconnects()
    {
        // Arrange
        var roomCode = "TEST";
        var connectionId = "new-host-connection";
        var previousDisconnectTime = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        var room = new Room
        {
            Code = roomCode,
            HostConnectionId = null,
            HostDisconnectedAtUtc = previousDisconnectTime
        };

        _roomStore.TryGetRoom(roomCode, out Arg.Any<Room?>())
            .Returns(x =>
            {
                x[1] = room;
                return true;
            });

        // Act
        await _sut.RegisterHostAsync(roomCode, connectionId);

        // Assert
        room.HostConnectionId.Should().Be(connectionId);
        room.HostDisconnectedAtUtc.Should().BeNull();
    }

    #endregion

    #region Test Helpers

    private class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    #endregion
}
