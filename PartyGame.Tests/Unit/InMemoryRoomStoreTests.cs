using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Server.Services;

namespace PartyGame.Tests.Unit;

/// <summary>
/// Unit tests for InMemoryRoomStore.
/// </summary>
public class InMemoryRoomStoreTests
{
    private readonly TestClock _clock;
    private readonly TestRoomCodeGenerator _codeGenerator;
    private readonly InMemoryRoomStore _store;

    public InMemoryRoomStoreTests()
    {
        _clock = new TestClock();
        _codeGenerator = new TestRoomCodeGenerator();
        _store = new InMemoryRoomStore(_codeGenerator, _clock);
    }

    [Fact]
    public void CreateRoom_ReturnsRoomWithGeneratedCode()
    {
        // Arrange
        _codeGenerator.NextCode = "TEST";

        // Act
        var room = _store.CreateRoom();

        // Assert
        room.Code.Should().Be("TEST");
    }

    [Fact]
    public void CreateRoom_SetsCreatedUtcFromClock()
    {
        // Arrange
        var expectedTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        _clock.UtcNow = expectedTime;
        _codeGenerator.NextCode = "ABCD";

        // Act
        var room = _store.CreateRoom();

        // Assert
        room.CreatedUtc.Should().Be(expectedTime);
    }

    [Fact]
    public void CreateRoom_SetsDefaultValues()
    {
        // Arrange
        _codeGenerator.NextCode = "ABCD";

        // Act
        var room = _store.CreateRoom();

        // Assert
        room.Status.Should().Be(RoomStatus.Lobby);
        room.IsLocked.Should().BeFalse();
        room.HostConnectionId.Should().BeNull();
        room.Players.Should().BeEmpty();
        room.MaxPlayers.Should().Be(8);
    }

    [Fact]
    public void TryGetRoom_WithExistingRoom_ReturnsTrue()
    {
        // Arrange
        _codeGenerator.NextCode = "WXYZ";
        _store.CreateRoom();

        // Act
        var found = _store.TryGetRoom("WXYZ", out var room);

        // Assert
        found.Should().BeTrue();
        room.Should().NotBeNull();
        room!.Code.Should().Be("WXYZ");
    }

    [Fact]
    public void TryGetRoom_WithNonExistingRoom_ReturnsFalse()
    {
        // Act
        var found = _store.TryGetRoom("NONE", out var room);

        // Assert
        found.Should().BeFalse();
        room.Should().BeNull();
    }

    [Fact]
    public void TryGetRoom_IsCaseInsensitive()
    {
        // Arrange
        _codeGenerator.NextCode = "ABCD";
        _store.CreateRoom();

        // Act
        var foundLower = _store.TryGetRoom("abcd", out var room1);
        var foundMixed = _store.TryGetRoom("AbCd", out var room2);

        // Assert
        foundLower.Should().BeTrue();
        foundMixed.Should().BeTrue();
        room1.Should().NotBeNull();
        room2.Should().NotBeNull();
    }

    [Fact]
    public void Update_ModifiesExistingRoom()
    {
        // Arrange
        _codeGenerator.NextCode = "UPDT";
        var room = _store.CreateRoom();
        room.IsLocked = true;
        room.Status = RoomStatus.InGame;

        // Act
        _store.Update(room);

        // Assert
        _store.TryGetRoom("UPDT", out var updatedRoom);
        updatedRoom!.IsLocked.Should().BeTrue();
        updatedRoom.Status.Should().Be(RoomStatus.InGame);
    }

    [Fact]
    public void Remove_DeletesRoom()
    {
        // Arrange
        _codeGenerator.NextCode = "DEL1";
        _store.CreateRoom();

        // Act
        _store.Remove("DEL1");

        // Assert
        _store.TryGetRoom("DEL1", out var room).Should().BeFalse();
        room.Should().BeNull();
    }

    [Fact]
    public void Remove_IsCaseInsensitive()
    {
        // Arrange
        _codeGenerator.NextCode = "RMCS";
        _store.CreateRoom();

        // Act
        _store.Remove("rmcs");

        // Assert
        _store.TryGetRoom("RMCS", out _).Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsAllRooms()
    {
        // Arrange
        _codeGenerator.NextCode = "AAA1";
        _store.CreateRoom();
        _codeGenerator.NextCode = "BBB2";
        _store.CreateRoom();
        _codeGenerator.NextCode = "CCC3";
        _store.CreateRoom();

        // Act
        var allRooms = _store.GetAll().ToList();

        // Assert
        allRooms.Should().HaveCount(3);
        allRooms.Select(r => r.Code).Should().Contain(new[] { "AAA1", "BBB2", "CCC3" });
    }

    [Fact]
    public void GetAll_ReturnsEmptyWhenNoRooms()
    {
        // Act
        var allRooms = _store.GetAll();

        // Assert
        allRooms.Should().BeEmpty();
    }

    #region Test Helpers

    private class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private class TestRoomCodeGenerator : IRoomCodeGenerator
    {
        public string NextCode { get; set; } = "TEST";

        public string Generate(ISet<string> existingCodes)
        {
            return NextCode;
        }
    }

    #endregion
}
