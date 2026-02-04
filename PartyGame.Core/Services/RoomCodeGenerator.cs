using PartyGame.Core.Interfaces;

namespace PartyGame.Core.Services;

/// <summary>
/// Generates random 4-character alphanumeric room codes.
/// Excludes ambiguous characters (0, O, I, 1, L) for readability.
/// </summary>
public class RoomCodeGenerator : IRoomCodeGenerator
{
    // Excludes ambiguous characters: 0, O, I, 1, L
    private const string AllowedCharacters = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;
    private const int MaxAttempts = 100;

    private readonly Random _random;

    public RoomCodeGenerator()
    {
        _random = new Random();
    }

    /// <summary>
    /// Constructor with seed for testing purposes.
    /// </summary>
    public RoomCodeGenerator(int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc />
    public string Generate(ISet<string> existingCodes)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = GenerateCode();
            if (!existingCodes.Contains(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            $"Failed to generate unique room code after {MaxAttempts} attempts. " +
            "Consider cleaning up old rooms.");
    }

    private string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
        {
            chars[i] = AllowedCharacters[_random.Next(AllowedCharacters.Length)];
        }
        return new string(chars);
    }
}
