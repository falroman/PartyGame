using PartyGame.Core.Models;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Storage interface for room management.
/// Allows swapping between in-memory and distributed storage (e.g., Redis).
/// </summary>
public interface IRoomStore
{
    /// <summary>
    /// Creates a new room with a unique code.
    /// </summary>
    /// <returns>The created room.</returns>
    Room CreateRoom();

    /// <summary>
    /// Attempts to retrieve a room by its code.
    /// </summary>
    /// <param name="code">The room code to look up.</param>
    /// <param name="room">The room if found, null otherwise.</param>
    /// <returns>True if the room was found, false otherwise.</returns>
    bool TryGetRoom(string code, out Room? room);

    /// <summary>
    /// Updates an existing room.
    /// </summary>
    /// <param name="room">The room to update.</param>
    void Update(Room room);

    /// <summary>
    /// Removes a room from storage.
    /// </summary>
    /// <param name="code">The room code to remove.</param>
    void Remove(string code);

    /// <summary>
    /// Gets all rooms (mainly for admin/debug purposes).
    /// </summary>
    /// <returns>All rooms in storage.</returns>
    IEnumerable<Room> GetAll();
}
