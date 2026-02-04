namespace PartyGame.Server.Options;

/// <summary>
/// Configuration options for room cleanup behavior.
/// </summary>
public class RoomCleanupOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RoomCleanup";

    /// <summary>
    /// Time in minutes before a room without an active host is removed.
    /// Default: 10 minutes.
    /// </summary>
    public int RoomWithoutHostTtlMinutes { get; set; } = 10;

    /// <summary>
    /// Time in seconds before a disconnected player is removed from the room.
    /// Default: 120 seconds (2 minutes).
    /// </summary>
    public int DisconnectedPlayerGraceSeconds { get; set; } = 120;

    /// <summary>
    /// Interval in seconds between cleanup runs.
    /// Default: 30 seconds.
    /// </summary>
    public int CleanupIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Whether cleanup is enabled. Set to false to disable automatic cleanup.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
