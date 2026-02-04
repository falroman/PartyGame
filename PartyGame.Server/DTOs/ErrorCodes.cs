namespace PartyGame.Server.DTOs;

/// <summary>
/// Centralized error codes used throughout the application.
/// These codes are machine-readable and sent to clients via ErrorDto.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Room with the specified code does not exist.</summary>
    public const string RoomNotFound = "ROOM_NOT_FOUND";
    
    /// <summary>Room is locked and not accepting new players.</summary>
    public const string RoomLocked = "ROOM_LOCKED";
    
    /// <summary>Room has reached its maximum player capacity.</summary>
    public const string RoomFull = "ROOM_FULL";
    
    /// <summary>Display name is empty or exceeds maximum length.</summary>
    public const string NameInvalid = "NAME_INVALID";
    
    /// <summary>Display name is already taken by another player in the room.</summary>
    public const string NameTaken = "NAME_TAKEN";
    
    /// <summary>Connection is already hosting another room.</summary>
    public const string AlreadyHost = "ALREADY_HOST";
    
    /// <summary>Action requires host privileges but caller is not the host.</summary>
    public const string NotHost = "NOT_HOST";
    
    /// <summary>Failed to establish connection to the server.</summary>
    public const string ConnectionFailed = "CONNECTION_FAILED";

    /// <summary>Room is not in the correct state for this action (e.g., game already running).</summary>
    public const string InvalidState = "INVALID_STATE";

    /// <summary>Not enough players to start the game (minimum 2 required).</summary>
    public const string NotEnoughPlayers = "NOT_ENOUGH_PLAYERS";
}
