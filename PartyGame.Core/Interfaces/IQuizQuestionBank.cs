using PartyGame.Core.Models.Quiz;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Interface for accessing quiz questions from a question bank.
/// </summary>
public interface IQuizQuestionBank
{
    /// <summary>
    /// Gets all questions for a specific locale.
    /// </summary>
    /// <param name="locale">The locale code (e.g., "nl-BE").</param>
    /// <returns>All questions matching the locale.</returns>
    IEnumerable<QuizQuestion> GetAll(string locale);

    /// <summary>
    /// Gets a random question matching the specified criteria.
    /// </summary>
    /// <param name="locale">The locale code (e.g., "nl-BE").</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="difficulty">Optional difficulty filter (1-5).</param>
    /// <param name="tags">Optional tags filter (question must have at least one matching tag).</param>
    /// <param name="excludeIds">Optional set of question IDs to exclude (already used).</param>
    /// <returns>A random question matching criteria, or null if none found.</returns>
    QuizQuestion? GetRandom(
        string locale,
        string? category = null,
        int? difficulty = null,
        IEnumerable<string>? tags = null,
        IEnumerable<string>? excludeIds = null);

    /// <summary>
    /// Tries to get a specific question by ID.
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <param name="question">The question if found.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetById(string questionId, out QuizQuestion? question);

    /// <summary>
    /// Gets all available locales in the question bank.
    /// </summary>
    /// <returns>Set of available locale codes.</returns>
    IReadOnlySet<string> GetAvailableLocales();

    /// <summary>
    /// Gets all available categories for a locale.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    /// <returns>Set of available categories.</returns>
    IReadOnlySet<string> GetAvailableCategories(string locale);

    /// <summary>
    /// Gets the total count of questions for a locale.
    /// </summary>
    /// <param name="locale">The locale code.</param>
    /// <returns>Number of questions available.</returns>
    int GetCount(string locale);
}
