namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO representing a player in a room.
/// </summary>
/// <param name="PlayerId">Unique player identifier.</param>
/// <param name="DisplayName">Player's display name.</param>
/// <param name="IsConnected">Whether the player is currently connected.</param>
/// <param name="Score">Player's current score.</param>
/// <param name="AvatarPresetId">Avatar preset ID if using preset.</param>
/// <param name="AvatarUrl">Avatar URL if using uploaded avatar.</param>
/// <param name="AvatarKind">Type of avatar.</param>
public record PlayerDto(
    Guid PlayerId,
    string DisplayName,
    bool IsConnected,
    int Score,
    string? AvatarPresetId,
    string? AvatarUrl,
    Core.Models.AvatarKind AvatarKind
);
