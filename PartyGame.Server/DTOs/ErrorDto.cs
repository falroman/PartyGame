namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO for error responses.
/// </summary>
/// <param name="Code">Machine-readable error code (e.g., ROOM_NOT_FOUND).</param>
/// <param name="Message">Human-readable error message.</param>
public record ErrorDto(string Code, string Message);
