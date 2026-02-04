namespace PartyGame.Server.DTOs;

/// <summary>
/// Response DTO for room creation.
/// </summary>
/// <param name="RoomCode">The generated room code.</param>
/// <param name="JoinUrl">URL for players to join (e.g., /join/A1B2).</param>
public record CreateRoomResponseDto(string RoomCode, string JoinUrl);
