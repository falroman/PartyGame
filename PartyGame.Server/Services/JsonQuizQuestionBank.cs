using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartyGame.Core.Exceptions;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Quiz;

namespace PartyGame.Server.Services;

/// <summary>
/// JSON file-based implementation of IQuizQuestionBank.
/// Loads question packs from the Content folder at startup.
/// </summary>
public class JsonQuizQuestionBank : IQuizQuestionBank
{
    private readonly ILogger<JsonQuizQuestionBank> _logger;
    private readonly Dictionary<string, List<QuizQuestion>> _questionsByLocale = new();
    private readonly Dictionary<string, QuizQuestion> _questionsById = new();
    private readonly HashSet<string> _availableLocales = new();
    private readonly Dictionary<string, HashSet<string>> _categoriesByLocale = new();
    private readonly Random _random = new();
    private bool _isLoaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public JsonQuizQuestionBank(IHostEnvironment environment, ILogger<JsonQuizQuestionBank> logger)
    {
        _logger = logger;
        LoadQuestionPacks(environment.ContentRootPath);
    }

    /// <summary>
    /// Constructor for testing with a specific content path.
    /// </summary>
    public JsonQuizQuestionBank(string contentPath, ILogger<JsonQuizQuestionBank> logger)
    {
        _logger = logger;
        LoadQuestionPacks(contentPath);
    }

    private void LoadQuestionPacks(string contentRootPath)
    {
        var contentFolder = Path.Combine(contentRootPath, "Content");
        
        if (!Directory.Exists(contentFolder))
        {
            _logger.LogWarning("Content folder not found at {ContentFolder}. No questions loaded.", contentFolder);
            _isLoaded = true;
            return;
        }

        var jsonFiles = Directory.GetFiles(contentFolder, "questions.*.json");
        
        if (jsonFiles.Length == 0)
        {
            _logger.LogWarning("No question pack files found in {ContentFolder}.", contentFolder);
            _isLoaded = true;
            return;
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                LoadQuestionPack(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load question pack from {File}", file);
                throw; // Fail fast on invalid packs
            }
        }

        _isLoaded = true;
        _logger.LogInformation(
            "Loaded {TotalQuestions} questions across {LocaleCount} locale(s): {Locales}",
            _questionsById.Count,
            _availableLocales.Count,
            string.Join(", ", _availableLocales));
    }

    private void LoadQuestionPack(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("Loading question pack from {File}...", fileName);

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            throw new QuestionPackValidationException($"Failed to read file: {ex.Message}", ex);
        }

        QuestionPack? pack;
        try
        {
            pack = JsonSerializer.Deserialize<QuestionPack>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new QuestionPackValidationException($"Invalid JSON in {fileName}: {ex.Message}", ex);
        }

        if (pack == null)
        {
            throw new QuestionPackValidationException($"Failed to deserialize {fileName}: result was null");
        }

        // Validate the pack
        ValidateQuestionPack(pack, fileName);

        // Index questions
        foreach (var question in pack.Questions)
        {
            // Use pack locale if question locale is empty
            if (string.IsNullOrEmpty(question.Locale))
            {
                question.Locale = pack.Locale;
            }

            var locale = question.Locale.ToLowerInvariant();
            
            if (!_questionsByLocale.TryGetValue(locale, out var localeQuestions))
            {
                localeQuestions = new List<QuizQuestion>();
                _questionsByLocale[locale] = localeQuestions;
                _categoriesByLocale[locale] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            localeQuestions.Add(question);
            _questionsById[question.Id] = question;
            _availableLocales.Add(locale);
            
            if (!string.IsNullOrEmpty(question.Category))
            {
                _categoriesByLocale[locale].Add(question.Category);
            }
        }

        _logger.LogInformation(
            "Loaded pack '{PackId}' ({Title}): {QuestionCount} questions for locale {Locale}",
            pack.PackId, pack.Title, pack.Questions.Count, pack.Locale);
    }

    private void ValidateQuestionPack(QuestionPack pack, string fileName)
    {
        var errors = new List<string>();

        // Pack-level validation
        if (string.IsNullOrWhiteSpace(pack.PackId))
            errors.Add("PackId is required");

        if (string.IsNullOrWhiteSpace(pack.Locale))
            errors.Add("Pack Locale is required");

        if (pack.Questions.Count == 0)
            errors.Add("Pack must contain at least one question");

        // Track IDs for uniqueness check
        var seenIds = new HashSet<string>();

        for (int i = 0; i < pack.Questions.Count; i++)
        {
            var q = pack.Questions[i];
            var prefix = $"Question[{i}]";

            // ID validation
            if (string.IsNullOrWhiteSpace(q.Id))
            {
                errors.Add($"{prefix}: Id is required");
            }
            else if (!seenIds.Add(q.Id))
            {
                errors.Add($"{prefix}: Duplicate Id '{q.Id}'");
            }

            // Question text
            if (string.IsNullOrWhiteSpace(q.Question))
                errors.Add($"{prefix} ({q.Id}): Question text is required");

            // Difficulty
            if (q.Difficulty < 1 || q.Difficulty > 5)
                errors.Add($"{prefix} ({q.Id}): Difficulty must be between 1 and 5 (was {q.Difficulty})");

            // Options validation
            if (q.Options.Count != 4)
            {
                errors.Add($"{prefix} ({q.Id}): Must have exactly 4 options (has {q.Options.Count})");
            }
            else
            {
                var optionKeys = new HashSet<string>();
                foreach (var opt in q.Options)
                {
                    if (string.IsNullOrWhiteSpace(opt.Key))
                        errors.Add($"{prefix} ({q.Id}): Option key cannot be empty");
                    else if (!optionKeys.Add(opt.Key))
                        errors.Add($"{prefix} ({q.Id}): Duplicate option key '{opt.Key}'");

                    if (string.IsNullOrWhiteSpace(opt.Text))
                        errors.Add($"{prefix} ({q.Id}): Option '{opt.Key}' text cannot be empty");
                }

                // Correct answer validation
                if (string.IsNullOrWhiteSpace(q.CorrectOptionKey))
                {
                    errors.Add($"{prefix} ({q.Id}): CorrectOptionKey is required");
                }
                else if (!optionKeys.Contains(q.CorrectOptionKey))
                {
                    errors.Add($"{prefix} ({q.Id}): CorrectOptionKey '{q.CorrectOptionKey}' does not match any option");
                }
            }

            // Locale consistency
            if (!string.IsNullOrEmpty(q.Locale) && 
                !q.Locale.Equals(pack.Locale, StringComparison.OrdinalIgnoreCase))
            {
                // This is a warning, not an error - questions can have different locales
                _logger.LogWarning(
                    "Question {QuestionId} has locale '{QuestionLocale}' different from pack locale '{PackLocale}'",
                    q.Id, q.Locale, pack.Locale);
            }
        }

        if (errors.Count > 0)
        {
            throw new QuestionPackValidationException(errors, pack.PackId);
        }
    }

    /// <inheritdoc />
    public IEnumerable<QuizQuestion> GetAll(string locale)
    {
        var normalizedLocale = locale.ToLowerInvariant();
        
        if (_questionsByLocale.TryGetValue(normalizedLocale, out var questions))
        {
            return questions;
        }

        return Enumerable.Empty<QuizQuestion>();
    }

    /// <inheritdoc />
    public QuizQuestion? GetRandom(
        string locale,
        string? category = null,
        int? difficulty = null,
        IEnumerable<string>? tags = null,
        IEnumerable<string>? excludeIds = null)
    {
        var candidates = GetAll(locale).AsEnumerable();

        // Apply filters
        if (!string.IsNullOrEmpty(category))
        {
            candidates = candidates.Where(q => 
                q.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (difficulty.HasValue)
        {
            candidates = candidates.Where(q => q.Difficulty == difficulty.Value);
        }

        if (tags != null && tags.Any())
        {
            var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            candidates = candidates.Where(q => q.Tags.Any(t => tagSet.Contains(t)));
        }

        if (excludeIds != null && excludeIds.Any())
        {
            var excludeSet = new HashSet<string>(excludeIds);
            candidates = candidates.Where(q => !excludeSet.Contains(q.Id));
        }

        var candidateList = candidates.ToList();
        
        if (candidateList.Count == 0)
        {
            return null;
        }

        return candidateList[_random.Next(candidateList.Count)];
    }

    /// <inheritdoc />
    public bool TryGetById(string questionId, out QuizQuestion? question)
    {
        return _questionsById.TryGetValue(questionId, out question);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetAvailableLocales()
    {
        return _availableLocales;
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetAvailableCategories(string locale)
    {
        var normalizedLocale = locale.ToLowerInvariant();
        
        if (_categoriesByLocale.TryGetValue(normalizedLocale, out var categories))
        {
            return categories;
        }

        return new HashSet<string>();
    }

    /// <inheritdoc />
    public int GetCount(string locale)
    {
        var normalizedLocale = locale.ToLowerInvariant();
        
        if (_questionsByLocale.TryGetValue(normalizedLocale, out var questions))
        {
            return questions.Count;
        }

        return 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRandomCategories(
        string locale,
        int count = 3,
        IEnumerable<string>? excludeCategories = null)
    {
        var normalizedLocale = locale.ToLowerInvariant();
        
        if (!_categoriesByLocale.TryGetValue(normalizedLocale, out var allCategories))
        {
            return Array.Empty<string>();
        }

        var availableCategories = allCategories.AsEnumerable();

        if (excludeCategories != null)
        {
            var excludeSet = new HashSet<string>(excludeCategories, StringComparer.OrdinalIgnoreCase);
            availableCategories = availableCategories.Where(c => !excludeSet.Contains(c));
        }

        var categoryList = availableCategories.ToList();
        
        if (categoryList.Count <= count)
        {
            // Return all available categories if we don't have enough
            return categoryList.OrderBy(_ => _random.Next()).ToList();
        }

        // Fisher-Yates shuffle and take first 'count' elements
        for (int i = categoryList.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (categoryList[i], categoryList[j]) = (categoryList[j], categoryList[i]);
        }

        return categoryList.Take(count).ToList();
    }

    /// <inheritdoc />
    public int GetCountByCategory(string locale, string category)
    {
        var normalizedLocale = locale.ToLowerInvariant();
        
        if (!_questionsByLocale.TryGetValue(normalizedLocale, out var questions))
        {
            return 0;
        }

        return questions.Count(q => 
            q.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }
}
