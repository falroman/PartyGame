namespace PartyGame.Core.Models.Boosters;

/// <summary>
/// Tracks a player's assigned booster and its usage state.
/// </summary>
public class PlayerBoosterState
{
    /// <summary>
    /// The type of booster assigned to this player.
    /// </summary>
    public required Enums.BoosterType Type { get; set; }

    /// <summary>
    /// Whether the booster has been used (consumed).
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// When the booster was activated (if used).
    /// </summary>
    public DateTime? ActivatedAtUtc { get; set; }

    /// <summary>
    /// The target player ID if the booster requires one.
    /// </summary>
    public Guid? TargetPlayerId { get; set; }

    /// <summary>
    /// The question number when this booster was activated.
    /// Used to track which question the effect applies to.
    /// </summary>
    public int? ActivatedOnQuestionNumber { get; set; }

    /// <summary>
    /// The round number when this booster was activated.
    /// Used for round-scoped effects like BackToZero.
    /// </summary>
    public int? ActivatedOnRoundNumber { get; set; }
}

/// <summary>
/// Represents an active booster effect that is currently applied.
/// </summary>
public class ActiveBoosterEffect
{
    /// <summary>
    /// The type of booster creating this effect.
    /// </summary>
    public required Enums.BoosterType BoosterType { get; set; }

    /// <summary>
    /// The player who activated this booster.
    /// </summary>
    public required Guid ActivatorPlayerId { get; set; }

    /// <summary>
    /// The target player (if applicable).
    /// </summary>
    public Guid? TargetPlayerId { get; set; }

    /// <summary>
    /// The question number this effect applies to.
    /// </summary>
    public int QuestionNumber { get; set; }

    /// <summary>
    /// The round number this effect applies to (for round-scoped effects).
    /// </summary>
    public int RoundNumber { get; set; }

    /// <summary>
    /// Additional data specific to the booster type.
    /// E.g., removed options for FiftyFifty, option order for ChaosMode.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Whether this effect has been fully applied and can be removed.
    /// </summary>
    public bool IsConsumed { get; set; }
}

/// <summary>
/// Result of a booster activation attempt.
/// </summary>
public class BoosterActivationResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The effect that was created (if successful).
    /// </summary>
    public ActiveBoosterEffect? Effect { get; set; }

    /// <summary>
    /// Whether a Shield blocked this activation (for negative boosters).
    /// </summary>
    public bool WasBlockedByShield { get; set; }

    /// <summary>
    /// The player whose Shield was consumed (if applicable).
    /// </summary>
    public Guid? ShieldBlockerPlayerId { get; set; }

    public static BoosterActivationResult Ok(ActiveBoosterEffect effect) => new()
    {
        Success = true,
        Effect = effect
    };

    public static BoosterActivationResult Blocked(Guid shieldOwnerId) => new()
    {
        Success = false,
        WasBlockedByShield = true,
        ShieldBlockerPlayerId = shieldOwnerId,
        ErrorCode = "BOOSTER_BLOCKED_BY_SHIELD",
        ErrorMessage = "Target's Shield blocked this booster."
    };

    public static BoosterActivationResult Error(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}

/// <summary>
/// DTO for booster information sent to clients.
/// </summary>
public class BoosterInfoDto
{
    public required Enums.BoosterType Type { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public bool IsUsed { get; set; }
    public bool RequiresTarget { get; set; }
    public string[] ValidPhases { get; set; } = [];
}

/// <summary>
/// Player-specific booster state for DTO.
/// </summary>
public class PlayerBoosterDto
{
    public required Guid PlayerId { get; set; }
    public required Enums.BoosterType BoosterType { get; set; }
    public bool IsUsed { get; set; }
}

/// <summary>
/// Event broadcasted when a booster is activated.
/// </summary>
public class BoosterActivatedEvent
{
    public required Enums.BoosterType BoosterType { get; set; }
    public required Guid ActivatorPlayerId { get; set; }
    public required string ActivatorName { get; set; }
    public Guid? TargetPlayerId { get; set; }
    public string? TargetName { get; set; }
    public bool WasBlockedByShield { get; set; }
    public Guid? ShieldBlockerPlayerId { get; set; }
    public string? ShieldBlockerName { get; set; }
}
