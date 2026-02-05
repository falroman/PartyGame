namespace PartyGame.Core.Models.Ranking;

/// <summary>
/// Represents a prompt for the Ranking Stars game.
/// </summary>
public class RankingPrompt
{
    /// <summary>
    /// Unique identifier for this prompt.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The prompt text (e.g., "Who would survive longest in a zombie apocalypse?").
    /// </summary>
    public string Prompt { get; init; } = string.Empty;
}

/// <summary>
/// Represents a raw ranking prompt item from JSON.
/// </summary>
public class RankingPromptItem
{
    /// <summary>
    /// Unique ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The prompt text.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Represents the JSON structure for the ranking stars pack.
/// </summary>
public class RankingStarsPack
{
    /// <summary>
    /// Schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Language/locale.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The prompt items.
    /// </summary>
    public List<RankingPromptItem> Items { get; set; } = new();
}

/// <summary>
/// Result of a ranking vote round.
/// </summary>
public class RankingVoteResult
{
    /// <summary>
    /// Player ID of the winner(s).
    /// </summary>
    public List<Guid> WinnerPlayerIds { get; set; } = new();

    /// <summary>
    /// Vote count for each player: PlayerId -> number of votes.
    /// </summary>
    public Dictionary<Guid, int> VoteCounts { get; set; } = new();

    /// <summary>
    /// Players who voted for the winner(s).
    /// </summary>
    public List<Guid> CorrectVoters { get; set; } = new();

    /// <summary>
    /// Maximum vote count (winner's votes).
    /// </summary>
    public int MaxVotes { get; set; }
}
