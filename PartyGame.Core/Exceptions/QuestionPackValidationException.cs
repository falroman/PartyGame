namespace PartyGame.Core.Exceptions;

/// <summary>
/// Exception thrown when question pack validation fails.
/// </summary>
public class QuestionPackValidationException : Exception
{
    /// <summary>
    /// List of validation errors found.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// The pack ID that failed validation (if available).
    /// </summary>
    public string? PackId { get; }

    public QuestionPackValidationException(string message, string? packId = null)
        : base(message)
    {
        PackId = packId;
        ValidationErrors = new List<string> { message };
    }

    public QuestionPackValidationException(IEnumerable<string> errors, string? packId = null)
        : base($"Question pack validation failed with {errors.Count()} error(s): {string.Join("; ", errors.Take(3))}")
    {
        PackId = packId;
        ValidationErrors = errors.ToList();
    }

    public QuestionPackValidationException(string message, Exception innerException, string? packId = null)
        : base(message, innerException)
    {
        PackId = packId;
        ValidationErrors = new List<string> { message };
    }
}
