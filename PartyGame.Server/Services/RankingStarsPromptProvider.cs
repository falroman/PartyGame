using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Ranking;
using System.Text.Json;

namespace PartyGame.Server.Services;

/// <summary>
/// Provides ranking stars prompts from JSON files.
/// </summary>
public class RankingStarsPromptProvider : IRankingStarsPromptProvider
{
    private readonly Dictionary<string, RankingStarsPack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<RankingStarsPromptProvider> _logger;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RankingStarsPromptProvider(ILogger<RankingStarsPromptProvider> logger)
    {
        _logger = logger;
        LoadPacks();
    }

    private void LoadPacks()
    {
        var contentPath = Path.Combine(AppContext.BaseDirectory, "Content");
        
        if (!Directory.Exists(contentPath))
        {
            _logger.LogWarning("Content directory not found at {Path}", contentPath);
            return;
        }

        var packFiles = Directory.GetFiles(contentPath, "rankingstars.*.json");

        foreach (var file in packFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var pack = JsonSerializer.Deserialize<RankingStarsPack>(json, JsonOptions);

                if (pack != null && pack.Items.Count > 0)
                {
                    var locale = ExtractLocaleFromFilename(file);
                    _packs[locale] = pack;
                    _logger.LogInformation("Loaded ranking stars pack for locale '{Locale}' with {Count} prompts from {File}",
                        locale, pack.Items.Count, file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ranking stars pack from {File}", file);
            }
        }

        if (_packs.Count == 0)
        {
            _logger.LogWarning("No ranking stars packs loaded. Ranking Stars game will not work.");
        }
    }

    private static string ExtractLocaleFromFilename(string filePath)
    {
        // Format: rankingstars.nl-BE.json -> nl-BE
        var filename = Path.GetFileNameWithoutExtension(filePath);
        var parts = filename.Split('.');
        return parts.Length >= 2 ? parts[1] : "unknown";
    }

    /// <inheritdoc />
    public IReadOnlyList<RankingPrompt> GetRandomPrompts(string locale, int count, IEnumerable<string>? excludeIds = null)
    {
        if (!_packs.TryGetValue(locale, out var pack))
        {
            _logger.LogWarning("No ranking stars pack found for locale '{Locale}'", locale);
            return Array.Empty<RankingPrompt>();
        }

        var excludeSet = excludeIds?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        
        var availableItems = pack.Items
            .Where(i => !excludeSet.Contains(i.Id))
            .ToList();

        if (availableItems.Count < count)
        {
            _logger.LogWarning("Not enough prompts available in locale '{Locale}'. Requested {Count}, available {Available}",
                locale, count, availableItems.Count);
            count = availableItems.Count;
        }

        return availableItems
            .OrderBy(_ => _random.Next())
            .Take(count)
            .Select(i => new RankingPrompt { Id = i.Id, Prompt = i.Prompt })
            .ToList();
    }

    /// <inheritdoc />
    public RankingPrompt? GetRandomPrompt(string locale, IEnumerable<string>? excludeIds = null)
    {
        var prompts = GetRandomPrompts(locale, 1, excludeIds);
        return prompts.FirstOrDefault();
    }

    /// <inheritdoc />
    public int GetCount(string locale)
    {
        return _packs.TryGetValue(locale, out var pack) ? pack.Items.Count : 0;
    }
}
