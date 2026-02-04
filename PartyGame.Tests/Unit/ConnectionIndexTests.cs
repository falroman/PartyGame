using PartyGame.Core.Enums;
using PartyGame.Server.Services;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for ConnectionIndex.
/// </summary>
public class ConnectionIndexTests
{
    private readonly ConnectionIndex _index;

    public ConnectionIndexTests()
    {
        _index = new ConnectionIndex();
    }

    [Fact]
    public void BindHost_CreatesBinding()
    {
        // Arrange
        var connectionId = "conn-123";
        var roomCode = "ABCD";

        // Act
        _index.BindHost(connectionId, roomCode);

        // Assert
        var found = _index.TryGet(connectionId, out var binding);
        found.Should().BeTrue();
        binding.Should().NotBeNull();
        binding!.ConnectionId.Should().Be(connectionId);
        binding.RoomCode.Should().Be("ABCD");
        binding.Role.Should().Be(ClientRole.Host);
        binding.PlayerId.Should().BeNull();
    }

    [Fact]
    public void BindHost_NormalizesRoomCodeToUpperCase()
    {
        // Arrange
        var connectionId = "conn-123";

        // Act
        _index.BindHost(connectionId, "abcd");

        // Assert
        _index.TryGet(connectionId, out var binding);
        binding!.RoomCode.Should().Be("ABCD");
    }

    [Fact]
    public void BindPlayer_CreatesBinding()
    {
        // Arrange
        var connectionId = "conn-456";
        var roomCode = "WXYZ";
        var playerId = Guid.NewGuid();

        // Act
        _index.BindPlayer(connectionId, roomCode, playerId);

        // Assert
        var found = _index.TryGet(connectionId, out var binding);
        found.Should().BeTrue();
        binding.Should().NotBeNull();
        binding!.ConnectionId.Should().Be(connectionId);
        binding.RoomCode.Should().Be("WXYZ");
        binding.Role.Should().Be(ClientRole.Player);
        binding.PlayerId.Should().Be(playerId);
    }

    [Fact]
    public void TryGet_WithNonExistingConnection_ReturnsFalse()
    {
        // Act
        var found = _index.TryGet("non-existent", out var binding);

        // Assert
        found.Should().BeFalse();
        binding.Should().BeNull();
    }

    [Fact]
    public void Unbind_RemovesBinding()
    {
        // Arrange
        var connectionId = "conn-123";
        _index.BindHost(connectionId, "ABCD");

        // Act
        _index.Unbind(connectionId);

        // Assert
        _index.TryGet(connectionId, out var binding).Should().BeFalse();
        binding.Should().BeNull();
    }

    [Fact]
    public void Unbind_WithNonExistingConnection_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var act = () => _index.Unbind("non-existent");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetBindingsForRoom_ReturnsAllBindingsForRoom()
    {
        // Arrange
        _index.BindHost("host-conn", "ROOM");
        _index.BindPlayer("player1-conn", "ROOM", Guid.NewGuid());
        _index.BindPlayer("player2-conn", "ROOM", Guid.NewGuid());
        _index.BindPlayer("other-conn", "OTHER", Guid.NewGuid());

        // Act
        var bindings = _index.GetBindingsForRoom("ROOM").ToList();

        // Assert
        bindings.Should().HaveCount(3);
        bindings.Select(b => b.ConnectionId).Should().Contain(new[] { "host-conn", "player1-conn", "player2-conn" });
    }

    [Fact]
    public void GetBindingsForRoom_IsCaseInsensitive()
    {
        // Arrange
        _index.BindHost("host-conn", "ROOM");

        // Act
        var bindings = _index.GetBindingsForRoom("room").ToList();

        // Assert
        bindings.Should().HaveCount(1);
    }

    [Fact]
    public void BindHost_OverwritesExistingBinding()
    {
        // Arrange
        var connectionId = "conn-123";
        _index.BindPlayer(connectionId, "OLD1", Guid.NewGuid());

        // Act
        _index.BindHost(connectionId, "NEW1");

        // Assert
        _index.TryGet(connectionId, out var binding);
        binding!.RoomCode.Should().Be("NEW1");
        binding.Role.Should().Be(ClientRole.Host);
    }
}
