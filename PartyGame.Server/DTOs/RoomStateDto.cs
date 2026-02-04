using PartyGame.Core.Enums;

namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO representing the current state of a room.
/// </summary>
/// <param name="RoomCode">The room code.</param>
/// <param name="Status">Current room status.</param>
/// <param name="IsLocked">Whether the room is locked.</param>
/// <param name="Players">List of players in the room.</param>
public record RoomStateDto(
    string RoomCode,
    RoomStatus Status,
    bool IsLocked,
    IReadOnlyList<PlayerDto> Players
);
