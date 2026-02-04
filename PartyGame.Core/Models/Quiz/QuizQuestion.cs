namespace PartyGame.Core.Models.Quiz;

/// <summary>
/// Represents a single quiz question with multiple choice options.
/// </summary>
public class QuizQuestion
{
    /// <summary>
    /// Unique identifier for this question within the pack.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category of the question (e.g., "Science", "History", "Geography").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Language/locale code (e.g., "nl-BE", "en-US").
    /// </summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>
    /// Difficulty level from 1 (easy) to 5 (expert).
    /// </summary>
    public int Difficulty { get; set; } = 1;

    /// <summary>
    /// The question text to display.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// List of answer options (typically 4).
    /// </summary>
    public List<QuizOption> Options { get; set; } = new();

    /// <summary>
    /// Key of the correct option (must match one of Options.Key).
    /// </summary>
    public string CorrectOptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional explanation shown after answering.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Time limit in seconds for this question (null = use default).
    /// </summary>
    public int? TimeLimitSeconds { get; set; }

    /// <summary>
    /// Whether to shuffle option order when displaying.
    /// </summary>
    public bool ShuffleOptions { get; set; } = true;

    /// <summary>
    /// Tags for filtering (e.g., "fun", "educational", "pop-culture").
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Source/attribution information.
    /// </summary>
    public SourceInfo Source { get; set; } = new();
}
