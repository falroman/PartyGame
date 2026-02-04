using System.Collections.Concurrent;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;

namespace PartyGame.Server.Services;

/// <summary>
/// In-memory implementation of IRoomStore using ConcurrentDictionary.
/// Suitable for development and single-server deployments.
/// </summary>
public class InMemoryRoomStore : IRoomStore
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly IRoomCodeGenerator _codeGenerator;
    private readonly IClock _clock;

    public InMemoryRoomStore(IRoomCodeGenerator codeGenerator, IClock clock)
    {
        _codeGenerator = codeGenerator;
        _clock = clock;
    }

    /// <inheritdoc />
    public Room CreateRoom()
    {
        var existingCodes = new HashSet<string>(_rooms.Keys);
        var code = _codeGenerator.Generate(existingCodes);

        var room = new Room
        {
            Code = code,
            CreatedUtc = _clock.UtcNow
        };

        if (!_rooms.TryAdd(code, room))
        {
            // Extremely rare race condition - retry
            return CreateRoom();
        }

        return room;
    }

    /// <inheritdoc />
    public bool TryGetRoom(string code, out Room? room)
    {
        return _rooms.TryGetValue(code.ToUpperInvariant(), out room);
    }

    /// <inheritdoc />
    public void Update(Room room)
    {
        _rooms[room.Code] = room;
    }

    /// <inheritdoc />
    public void Remove(string code)
    {
        _rooms.TryRemove(code.ToUpperInvariant(), out _);
    }

    /// <inheritdoc />
    public IEnumerable<Room> GetAll()
    {
        return _rooms.Values;
    }
}
