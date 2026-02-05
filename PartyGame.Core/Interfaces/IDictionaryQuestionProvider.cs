using PartyGame.Core.Models.Dictionary;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Provides dictionary questions for the Dictionary Game round.
/// </summary>
public interface IDictionaryQuestionProvider
{
    /// <summary>
    /// Gets a random dictionary question, excluding already used words.
    /// </summary>
    /// <param name="locale">The locale (e.g., "nl-BE").</param>
    /// <param name="usedWords">Words to exclude (already used in this game).</param>
    /// <returns>A dictionary question with one correct definition and three distractors, or null if no words available.</returns>
    DictionaryQuestion? GetRandomQuestion(string locale, IEnumerable<string>? usedWords = null);

    /// <summary>
    /// Gets the total count of available words for a locale.
    /// </summary>
    int GetWordCount(string locale);
}
