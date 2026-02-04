namespace PartyGame.Core.Interfaces;

/// <summary>
/// Generates unique room codes.
/// </summary>
public interface IRoomCodeGenerator
{
    /// <summary>
    /// Generates a new room code.
    /// </summary>
    /// <param name="existingCodes">Set of codes that already exist (to avoid collisions).</param>
    /// <returns>A unique room code.</returns>
    string Generate(ISet<string> existingCodes);
}
