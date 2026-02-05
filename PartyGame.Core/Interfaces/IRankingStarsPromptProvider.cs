using PartyGame.Core.Models.Ranking;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Provides prompts for the Ranking Stars game.
/// </summary>
public interface IRankingStarsPromptProvider
{
    /// <summary>
    /// Gets random prompts, excluding already used ones.
    /// </summary>
    /// <param name="locale">The locale (e.g., "nl-BE").</param>
    /// <param name="count">Number of prompts to get.</param>
    /// <param name="excludeIds">Prompt IDs to exclude (already used).</param>
    /// <returns>List of prompts, or empty list if not enough available.</returns>
    IReadOnlyList<RankingPrompt> GetRandomPrompts(string locale, int count, IEnumerable<string>? excludeIds = null);

    /// <summary>
    /// Gets a single random prompt.
    /// </summary>
    /// <param name="locale">The locale.</param>
    /// <param name="excludeIds">Prompt IDs to exclude.</param>
    /// <returns>A prompt, or null if none available.</returns>
    RankingPrompt? GetRandomPrompt(string locale, IEnumerable<string>? excludeIds = null);

    /// <summary>
    /// Gets the total count of available prompts for a locale.
    /// </summary>
    int GetCount(string locale);
}
