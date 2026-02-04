namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO representing a player in a room.
/// </summary>
/// <param name="PlayerId">Unique player identifier.</param>
/// <param name="DisplayName">Player's display name.</param>
/// <param name="IsConnected">Whether the player is currently connected.</param>
/// <param name="Score">Player's current score.</param>
public record PlayerDto(
    Guid PlayerId,
    string DisplayName,
    bool IsConnected,
    int Score
);
