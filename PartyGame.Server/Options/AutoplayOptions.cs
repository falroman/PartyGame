namespace PartyGame.Server.Options;

/// <summary>
/// Configuration options for server-side bot autoplay.
/// </summary>
public class AutoplayOptions
{
    public const string SectionName = "Autoplay";

    /// <summary>
    /// Enables autoplay in the current environment (Development only).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Minimum number of bots to add when choosing a random count.
    /// </summary>
    public int MinBots { get; set; } = 4;

    /// <summary>
    /// Maximum number of bots to add when choosing a random count.
    /// </summary>
    public int MaxBots { get; set; } = 12;

    /// <summary>
    /// Polling interval for the autoplay loop.
    /// </summary>
    public int PollIntervalMs { get; set; } = 250;

    /// <summary>
    /// Minimum delay before a bot performs an action.
    /// </summary>
    public int MinActionDelayMs { get; set; } = 250;

    /// <summary>
    /// Maximum delay before a bot performs an action.
    /// </summary>
    public int MaxActionDelayMs { get; set; } = 1200;

    /// <summary>
    /// Default time limit in seconds (fallback for actions).
    /// </summary>
    public int DefaultTimeLimitSeconds { get; set; } = 15;
}
