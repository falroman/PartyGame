namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO representing a game session.
/// </summary>
/// <param name="GameType">The type of game (e.g., "Quiz").</param>
/// <param name="Phase">Current phase of the game (e.g., "Starting", "Question").</param>
/// <param name="StartedUtc">When the game session started (ISO 8601 format).</param>
public record GameSessionDto(
    string GameType,
    string Phase,
    DateTime StartedUtc
);
