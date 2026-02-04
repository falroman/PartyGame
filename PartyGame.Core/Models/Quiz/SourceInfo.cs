namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Source information for a quiz question (for attribution/credits).
/// </summary>
public class SourceInfo
{
    /// <summary>
    /// Type of source (e.g., "original", "wikipedia", "trivia-api").
    /// </summary>
    public string Type { get; set; } = "original";

    /// <summary>
    /// Reference URL or identifier (optional).
    /// </summary>
    public string? Ref { get; set; }
}
