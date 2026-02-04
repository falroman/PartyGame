using PartyGame.Core.Enums;

namespace PartyGame.Core.Models;

/// <summary>
/// Represents an active game session within a room.
/// </summary>
public class GameSession
{
    /// <summary>
    /// The type of game being played.
    /// </summary>
    public GameType GameType { get; set; }

    /// <summary>
    /// Current phase of the game (e.g., "Starting", "Question", "Voting", "Results").
    /// Using string for flexibility across different game types.
    /// </summary>
    public string Phase { get; set; } = "Starting";

    /// <summary>
    /// When the game session started.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Game-specific state data. Can be null during initial phases.
    /// Each game type can define its own state structure.
    /// </summary>
    public object? State { get; set; }
}
