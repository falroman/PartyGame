using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Dictionary;
using System.Text.Json;

namespace PartyGame.Server.Services;

/// <summary>
/// Provides dictionary questions from JSON files.
/// </summary>
public class DictionaryQuestionProvider : IDictionaryQuestionProvider
{
    private readonly Dictionary<string, DictionaryPack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<DictionaryQuestionProvider> _logger;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DictionaryQuestionProvider(ILogger<DictionaryQuestionProvider> logger)
    {
        _logger = logger;
        LoadDictionaryPacks();
    }

    private void LoadDictionaryPacks()
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "Content");
        
        if (!Directory.Exists(contentPath))
        {
            _logger.LogWarning("Content directory not found at {Path}", contentPath);
            return;
        }

        var dictionaryFiles = Directory.GetFiles(contentPath, "dictionary.*.json");

        foreach (var file in dictionaryFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var pack = JsonSerializer.Deserialize<DictionaryPack>(json, JsonOptions);

                if (pack != null && pack.Items.Count > 0)
                {
                    var locale = ExtractLocaleFromFilename(file);
                    _packs[locale] = pack;
                    _logger.LogInformation("Loaded dictionary pack for locale '{Locale}' with {Count} words from {File}",
                        locale, pack.Items.Count, file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dictionary pack from {File}", file);
            }
        }

        if (_packs.Count == 0)
        {
            _logger.LogWarning("No dictionary packs loaded. Dictionary game will not work.");
        }
    }

    private static string ExtractLocaleFromFilename(string filePath)
    {
        // Format: dictionary.nl-BE.json -> nl-BE
        var filename = Path.GetFileNameWithoutExtension(filePath);
        var parts = filename.Split('.');
        return parts.Length >= 2 ? parts[1] : "unknown";
    }

    /// <inheritdoc />
    public DictionaryQuestion? GetRandomQuestion(string locale, IEnumerable<string>? usedWords = null)
    {
        if (!_packs.TryGetValue(locale, out var pack))
        {
            _logger.LogWarning("No dictionary pack found for locale '{Locale}'", locale);
            return null;
        }

        var usedSet = usedWords?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        
        // Get available items (not yet used)
        var availableItems = pack.Items
            .Where(i => !usedSet.Contains(i.Word))
            .ToList();

        if (availableItems.Count == 0)
        {
            _logger.LogWarning("No available words left in dictionary pack for locale '{Locale}'", locale);
            return null;
        }

        // Pick a random word
        var selectedIndex = _random.Next(availableItems.Count);
        var selectedItem = availableItems[selectedIndex];

        // Get 3 random distractor definitions (from other words)
        var otherDefinitions = pack.Items
            .Where(i => !i.Word.Equals(selectedItem.Word, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Definition)
            .OrderBy(_ => _random.Next())
            .Take(3)
            .ToList();

        if (otherDefinitions.Count < 3)
        {
            _logger.LogWarning("Not enough distractor definitions available for word '{Word}'", selectedItem.Word);
            return null;
        }

        // Build options: correct definition + 3 distractors
        var options = new List<string> { selectedItem.Definition };
        options.AddRange(otherDefinitions);

        // Shuffle options
        var shuffledOptions = options.OrderBy(_ => _random.Next()).ToList();
        var correctIndex = shuffledOptions.IndexOf(selectedItem.Definition);

        return new DictionaryQuestion
        {
            Word = selectedItem.Word,
            Options = shuffledOptions,
            CorrectIndex = correctIndex,
            Definition = selectedItem.Definition
        };
    }

    /// <inheritdoc />
    public int GetWordCount(string locale)
    {
        return _packs.TryGetValue(locale, out var pack) ? pack.Items.Count : 0;
    }
}
