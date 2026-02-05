namespace PartyGame.Core.Models.Dictionary;

/// <summary>
/// Represents a dictionary question with a word and multiple definition options.
/// </summary>
public class DictionaryQuestion
{
    /// <summary>
    /// The word to define.
    /// </summary>
    public string Word { get; init; } = string.Empty;

    /// <summary>
    /// The 4 definition options (one correct, three distractors).
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Index of the correct definition (0-3).
    /// </summary>
    public int CorrectIndex { get; init; }

    /// <summary>
    /// The correct definition text (for display in reveal phase).
    /// </summary>
    public string Definition { get; init; } = string.Empty;
}

/// <summary>
/// Represents a raw dictionary item from JSON.
/// </summary>
public class DictionaryItem
{
    /// <summary>
    /// The word.
    /// </summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// The definition of the word.
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}

/// <summary>
/// Represents the JSON structure for the dictionary pack.
/// </summary>
public class DictionaryPack
{
    /// <summary>
    /// Schema version.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Type of content.
    /// </summary>
    public string Type { get; set; } = "dictionary-game";

    /// <summary>
    /// Language/locale.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The dictionary items.
    /// </summary>
    public List<DictionaryItem> Items { get; set; } = new();
}
