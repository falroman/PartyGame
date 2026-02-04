namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents a single answer option for a quiz question.
/// </summary>
public class QuizOption
{
    /// <summary>
    /// Unique key for this option (e.g., "A", "B", "C", "D").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The text displayed for this option.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
