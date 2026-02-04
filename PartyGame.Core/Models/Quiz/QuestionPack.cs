namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents a collection of quiz questions loaded from a JSON file.
/// </summary>
public class QuestionPack
{
    /// <summary>
    /// Schema version for forward compatibility.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Unique identifier for this pack.
    /// </summary>
    public string PackId { get; set; } = string.Empty;

    /// <summary>
    /// Display title of the pack.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Primary locale of this pack (e.g., "nl-BE").
    /// </summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// Optional tags for the entire pack.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// List of questions in this pack.
    /// </summary>
    public List<QuizQuestion> Questions { get; set; } = new();
}
