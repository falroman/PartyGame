using PartyGame.Core.Enums;
using PartyGame.Core.Models.Boosters;
using PartyGame.Core.Models.Quiz;
using PartyGame.Core.Models.Scoring;

namespace PartyGame.Core.Interfaces;

/// <summary>
/// Handler interface for individual booster types.
/// Enables plug-in style booster implementation.
/// </summary>
public interface IBoosterHandler
{
    /// <summary>
    /// The booster type this handler manages.
    /// </summary>
    BoosterType Type { get; }

    /// <summary>
    /// Display name of the booster.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// User-friendly description of what the booster does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this booster requires selecting a target player.
    /// </summary>
    bool RequiresTarget { get; }

    /// <summary>
    /// Whether this booster is a passive effect (like Shield).
    /// </summary>
    bool IsPassive { get; }

    /// <summary>
    /// Whether this booster has negative effects on the target.
    /// Used to determine if Shield can block it.
    /// </summary>
    bool IsNegative { get; }

    /// <summary>
    /// Valid phases in which this booster can be activated.
    /// </summary>
    QuizPhase[] ValidPhases { get; }

    /// <summary>
    /// Validates if the booster can be activated in current state.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="activatorId">Player activating the booster.</param>
    /// <param name="targetId">Target player (if applicable).</param>
    /// <returns>Error message if invalid, null if valid.</returns>
    string? Validate(QuizGameState state, Guid activatorId, Guid? targetId);

    /// <summary>
    /// Applies the booster effect to the game state.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="activatorId">Player activating the booster.</param>
    /// <param name="targetId">Target player (if applicable).</param>
    /// <returns>The created effect.</returns>
    ActiveBoosterEffect Apply(QuizGameState state, Guid activatorId, Guid? targetId);
}

/// <summary>
/// Service for managing boosters throughout the game lifecycle.
/// </summary>
public interface IBoosterService
{
    /// <summary>
    /// Assigns random boosters to all players at game start.
    /// Each player gets exactly one booster.
    /// </summary>
    /// <param name="state">The game state to modify.</param>
    void AssignBoostersAtGameStart(QuizGameState state);

    /// <summary>
    /// Assigns a specific booster to a player (for testing).
    /// </summary>
    /// <param name="state">The game state.</param>
    /// <param name="playerId">The player to assign to.</param>
    /// <param name="boosterType">The booster type to assign.</param>
    void AssignBooster(QuizGameState state, Guid playerId, BoosterType boosterType);

    /// <summary>
    /// Checks if a player can activate their booster.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="playerId">Player attempting activation.</param>
    /// <param name="boosterType">Expected booster type.</param>
    /// <param name="targetId">Target player (if applicable).</param>
    /// <returns>Validation result.</returns>
    (bool CanActivate, string? ErrorCode, string? ErrorMessage) CanActivateBooster(
        QuizGameState state, Guid playerId, BoosterType boosterType, Guid? targetId);

    /// <summary>
    /// Activates a player's booster.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="playerId">Player activating.</param>
    /// <param name="boosterType">Booster to activate.</param>
    /// <param name="targetId">Target player (if applicable).</param>
    /// <returns>Activation result.</returns>
    BoosterActivationResult ActivateBooster(
        QuizGameState state, Guid playerId, BoosterType boosterType, Guid? targetId);

    /// <summary>
    /// Gets booster info for a player.
    /// </summary>
    BoosterInfoDto? GetBoosterInfo(QuizGameState state, Guid playerId);

    /// <summary>
    /// Gets all player booster states (for DTO).
    /// </summary>
    List<PlayerBoosterDto> GetPlayerBoosters(QuizGameState state);

    /// <summary>
    /// Gets all active effects for current question.
    /// </summary>
    List<ActiveBoosterEffect> GetActiveEffects(QuizGameState state);

    /// <summary>
    /// Applies pre-answering booster effects (Nope, ChaosMode).
    /// Called when entering Answering phase.
    /// </summary>
    void ApplyPreAnsweringEffects(QuizGameState state);

    /// <summary>
    /// Applies effects during answering (50/50, LateLock, Mirror, Wildcard).
    /// Returns player-specific option modifications.
    /// </summary>
    Dictionary<Guid, AnsweringEffects> GetAnsweringEffects(QuizGameState state);

    /// <summary>
    /// Applies booster effects after scoring (DoublePoints, PositionSwitch, BackToZero).
    /// Modifies the scoring results.
    /// </summary>
    void ApplyPostScoringEffects(QuizGameState state, List<QuestionScoreResult> results);

    /// <summary>
    /// Cleans up effects after a question is complete.
    /// </summary>
    void CleanupQuestionEffects(QuizGameState state);

    /// <summary>
    /// Gets the handler for a specific booster type.
    /// </summary>
    IBoosterHandler? GetHandler(BoosterType type);
}

/// <summary>
/// Player-specific effects during answering phase.
/// </summary>
public class AnsweringEffects
{
    /// <summary>
    /// Whether the player is blocked from answering (Nope).
    /// </summary>
    public bool IsNoped { get; set; }

    /// <summary>
    /// Options that are disabled/removed (FiftyFifty).
    /// </summary>
    public List<string> RemovedOptions { get; set; } = new();

    /// <summary>
    /// Custom option order (ChaosMode).
    /// </summary>
    public List<string>? ShuffledOptionOrder { get; set; }

    /// <summary>
    /// Extended deadline for this player (LateLock).
    /// </summary>
    public DateTime? ExtendedDeadline { get; set; }

    /// <summary>
    /// Player whose answer to copy (Mirror).
    /// </summary>
    public Guid? MirrorTargetId { get; set; }

    /// <summary>
    /// Whether this player can change their answer (Wildcard).
    /// </summary>
    public bool CanChangeAnswer { get; set; }
}
