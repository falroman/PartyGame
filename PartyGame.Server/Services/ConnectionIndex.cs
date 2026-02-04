using System.Collections.Concurrent;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;

namespace PartyGame.Server.Services;

/// <summary>
/// Thread-safe in-memory implementation of IConnectionIndex.
/// Tracks SignalR connections and their room/player bindings.
/// </summary>
public class ConnectionIndex : IConnectionIndex
{
    private readonly ConcurrentDictionary<string, ConnectionBinding> _bindings = new();

    /// <inheritdoc />
    public void BindHost(string connectionId, string roomCode)
    {
        var binding = new ConnectionBinding
        {
            ConnectionId = connectionId,
            RoomCode = roomCode.ToUpperInvariant(),
            Role = ClientRole.Host,
            PlayerId = null
        };

        _bindings[connectionId] = binding;
    }

    /// <inheritdoc />
    public void BindPlayer(string connectionId, string roomCode, Guid playerId)
    {
        var binding = new ConnectionBinding
        {
            ConnectionId = connectionId,
            RoomCode = roomCode.ToUpperInvariant(),
            Role = ClientRole.Player,
            PlayerId = playerId
        };

        _bindings[connectionId] = binding;
    }

    /// <inheritdoc />
    public bool TryGet(string connectionId, out ConnectionBinding? binding)
    {
        return _bindings.TryGetValue(connectionId, out binding);
    }

    /// <inheritdoc />
    public void Unbind(string connectionId)
    {
        _bindings.TryRemove(connectionId, out _);
    }

    /// <inheritdoc />
    public IEnumerable<ConnectionBinding> GetBindingsForRoom(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        return _bindings.Values.Where(b => b.RoomCode == normalizedCode);
    }
}
