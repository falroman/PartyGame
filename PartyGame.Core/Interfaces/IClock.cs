namespace PartyGame.Core.Interfaces;

/// <summary>
/// Abstraction for getting the current time.
/// Allows for testable time-dependent code.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }
}

/// <summary>
/// Default implementation using the system clock.
/// </summary>
public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
