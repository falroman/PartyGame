using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models.Boosters;
using PartyGame.Core.Models.Quiz;
using PartyGame.Core.Models.Scoring;

namespace PartyGame.Server.Services.Boosters;

/// <summary>
/// Service for managing boosters throughout the game lifecycle.
/// </summary>
public class BoosterService : IBoosterService
{
    private readonly Dictionary<BoosterType, IBoosterHandler> _handlers;
    private readonly ILogger<BoosterService> _logger;

    // Boosters available for random assignment (excludes Shield initially for balance)
    private static readonly BoosterType[] AssignableBoosters =
    [
        BoosterType.DoublePoints,
        BoosterType.FiftyFifty,
        BoosterType.BackToZero,
        BoosterType.Nope,
        BoosterType.PositionSwitch,
        BoosterType.LateLock,
        BoosterType.Mirror,
        BoosterType.ChaosMode,
        BoosterType.Shield
    ];

    public BoosterService(IEnumerable<IBoosterHandler> handlers, ILogger<BoosterService> logger)
    {
        _handlers = handlers.ToDictionary(h => h.Type);
        _logger = logger;
    }

    /// <inheritdoc />
    public void AssignBoostersAtGameStart(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var random = new Random();
        var playerIds = state.Scoreboard.Select(p => p.PlayerId).ToList();

        // Shuffle boosters and assign one to each player
        var shuffledBoosters = AssignableBoosters
            .OrderBy(_ => random.Next())
            .Take(playerIds.Count)
            .ToList();

        // If we have more players than boosters, cycle through
        for (int i = 0; i < playerIds.Count; i++)
        {
            var boosterType = shuffledBoosters[i % shuffledBoosters.Count];
            state.PlayerBoosters[playerIds[i]] = new PlayerBoosterState
            {
                Type = boosterType,
                IsUsed = false
            };
        }

        _logger.LogInformation("Assigned boosters to {PlayerCount} players in room {RoomCode}",
            playerIds.Count, state.RoomCode);
    }

    /// <inheritdoc />
    public void AssignBooster(QuizGameState state, Guid playerId, BoosterType boosterType)
    {
        state.PlayerBoosters[playerId] = new PlayerBoosterState
        {
            Type = boosterType,
            IsUsed = false
        };
    }

    /// <inheritdoc />
    public (bool CanActivate, string? ErrorCode, string? ErrorMessage) CanActivateBooster(
        QuizGameState state, Guid playerId, BoosterType boosterType, Guid? targetId)
    {
        // Check player has a booster
        if (!state.PlayerBoosters.TryGetValue(playerId, out var boosterState))
            return (false, "BOOSTER_NOT_OWNED", "You don't have a booster.");

        // Check it's the right type
        if (boosterState.Type != boosterType)
            return (false, "BOOSTER_NOT_OWNED", $"You don't have a {boosterType} booster.");

        // Check not already used
        if (boosterState.IsUsed)
            return (false, "BOOSTER_ALREADY_USED", "Your booster has already been used.");

        // Get handler and validate
        if (!_handlers.TryGetValue(boosterType, out var handler))
            return (false, "BOOSTER_INVALID", "Unknown booster type.");

        var validationError = handler.Validate(state, playerId, targetId);
        if (validationError != null)
        {
            var errorCode = validationError.Contains("phase") 
                ? "BOOSTER_INVALID_PHASE" 
                : validationError.Contains("target") 
                    ? "BOOSTER_INVALID_TARGET" 
                    : "BOOSTER_INVALID";
            return (false, errorCode, validationError);
        }

        return (true, null, null);
    }

    /// <inheritdoc />
    public BoosterActivationResult ActivateBooster(
        QuizGameState state, Guid playerId, BoosterType boosterType, Guid? targetId)
    {
        var (canActivate, errorCode, errorMessage) = CanActivateBooster(state, playerId, boosterType, targetId);
        if (!canActivate)
            return BoosterActivationResult.Error(errorCode!, errorMessage!);

        var handler = _handlers[boosterType];

        // Check if target has Shield (for negative boosters)
        if (handler.IsNegative && targetId.HasValue)
        {
            if (TryConsumeShield(state, targetId.Value, out var shieldOwnerId))
            {
                // Mark activator's booster as used even though it was blocked
                state.PlayerBoosters[playerId].IsUsed = true;
                state.PlayerBoosters[playerId].ActivatedAtUtc = DateTime.UtcNow;
                
                _logger.LogInformation("Booster {BoosterType} from {ActivatorId} was blocked by {TargetId}'s Shield",
                    boosterType, playerId, shieldOwnerId);
                
                return BoosterActivationResult.Blocked(shieldOwnerId);
            }
        }

        // Apply the booster
        var effect = handler.Apply(state, playerId, targetId);
        state.ActiveEffects.Add(effect);

        // Mark as used
        var playerBooster = state.PlayerBoosters[playerId];
        playerBooster.IsUsed = true;
        playerBooster.ActivatedAtUtc = DateTime.UtcNow;
        playerBooster.TargetPlayerId = targetId;
        playerBooster.ActivatedOnQuestionNumber = state.QuestionNumber;
        playerBooster.ActivatedOnRoundNumber = state.RoundNumber;

        _logger.LogInformation("Booster {BoosterType} activated by {PlayerId} targeting {TargetId} in room {RoomCode}",
            boosterType, playerId, targetId, state.RoomCode);

        return BoosterActivationResult.Ok(effect);
    }

    /// <inheritdoc />
    public BoosterInfoDto? GetBoosterInfo(QuizGameState state, Guid playerId)
    {
        if (!state.PlayerBoosters.TryGetValue(playerId, out var boosterState))
            return null;

        if (!_handlers.TryGetValue(boosterState.Type, out var handler))
            return null;

        return new BoosterInfoDto
        {
            Type = boosterState.Type,
            Name = handler.Name,
            Description = handler.Description,
            IsUsed = boosterState.IsUsed,
            RequiresTarget = handler.RequiresTarget,
            ValidPhases = handler.ValidPhases.Select(p => p.ToString()).ToArray()
        };
    }

    /// <inheritdoc />
    public List<PlayerBoosterDto> GetPlayerBoosters(QuizGameState state)
    {
        return state.PlayerBoosters.Select(kvp => new PlayerBoosterDto
        {
            PlayerId = kvp.Key,
            BoosterType = kvp.Value.Type,
            IsUsed = kvp.Value.IsUsed
        }).ToList();
    }

    /// <inheritdoc />
    public List<ActiveBoosterEffect> GetActiveEffects(QuizGameState state)
    {
        return state.ActiveEffects
            .Where(e => e.QuestionNumber == state.QuestionNumber && !e.IsConsumed)
            .ToList();
    }

    /// <inheritdoc />
    public void ApplyPreAnsweringEffects(QuizGameState state)
    {
        // Nope effects are already tracked in ActiveEffects
        // ChaosMode shuffled orders are stored in effect Data
        // No additional state modification needed here
    }

    /// <inheritdoc />
    public Dictionary<Guid, AnsweringEffects> GetAnsweringEffects(QuizGameState state)
    {
        var effects = new Dictionary<Guid, AnsweringEffects>();

        // Initialize for all players
        foreach (var player in state.Scoreboard)
        {
            effects[player.PlayerId] = new AnsweringEffects();
        }

        foreach (var effect in GetActiveEffects(state))
        {
            switch (effect.BoosterType)
            {
                case BoosterType.Nope:
                    if (effect.TargetPlayerId.HasValue && effects.ContainsKey(effect.TargetPlayerId.Value))
                    {
                        effects[effect.TargetPlayerId.Value].IsNoped = true;
                    }
                    break;

                case BoosterType.FiftyFifty:
                    if (effects.ContainsKey(effect.ActivatorPlayerId) && 
                        effect.Data.TryGetValue("RemovedOptions", out var removedObj) &&
                        removedObj is List<string> removed)
                    {
                        effects[effect.ActivatorPlayerId].RemovedOptions = removed;
                    }
                    break;

                case BoosterType.ChaosMode:
                    if (effect.Data.TryGetValue("ShuffledOrders", out var ordersObj) &&
                        ordersObj is Dictionary<Guid, List<string>> orders)
                    {
                        foreach (var (playerId, order) in orders)
                        {
                            if (effects.ContainsKey(playerId))
                            {
                                effects[playerId].ShuffledOptionOrder = order;
                            }
                        }
                    }
                    break;

                case BoosterType.LateLock:
                    if (effects.ContainsKey(effect.ActivatorPlayerId) &&
                        effect.Data.TryGetValue("ExtendedDeadline", out var deadlineObj) &&
                        deadlineObj is DateTime deadline)
                    {
                        effects[effect.ActivatorPlayerId].ExtendedDeadline = deadline;
                    }
                    break;

                case BoosterType.Mirror:
                    if (effects.ContainsKey(effect.ActivatorPlayerId) && effect.TargetPlayerId.HasValue)
                    {
                        effects[effect.ActivatorPlayerId].MirrorTargetId = effect.TargetPlayerId;
                    }
                    break;
            }
        }

        return effects;
    }

    /// <inheritdoc />
    public void ApplyPostScoringEffects(QuizGameState state, List<QuestionScoreResult> results)
    {
        foreach (var effect in GetActiveEffects(state))
        {
            switch (effect.BoosterType)
            {
                case BoosterType.DoublePoints:
                    ApplyDoublePoints(effect, results);
                    break;

                case BoosterType.PositionSwitch:
                    ApplyPositionSwitch(state, effect, results);
                    break;

                case BoosterType.BackToZero:
                    ApplyBackToZero(state, effect);
                    break;
            }

            effect.IsConsumed = true;
        }
    }

    /// <inheritdoc />
    public void CleanupQuestionEffects(QuizGameState state)
    {
        state.ActiveEffects.RemoveAll(e => e.IsConsumed || e.QuestionNumber < state.QuestionNumber);
    }

    /// <inheritdoc />
    public IBoosterHandler? GetHandler(BoosterType type)
    {
        return _handlers.TryGetValue(type, out var handler) ? handler : null;
    }

    #region Private Helper Methods

    private bool TryConsumeShield(QuizGameState state, Guid targetId, out Guid shieldOwnerId)
    {
        shieldOwnerId = Guid.Empty;

        if (!state.PlayerBoosters.TryGetValue(targetId, out var targetBooster))
            return false;

        if (targetBooster.Type != BoosterType.Shield || targetBooster.IsUsed)
            return false;

        // Consume the shield
        targetBooster.IsUsed = true;
        targetBooster.ActivatedAtUtc = DateTime.UtcNow;
        shieldOwnerId = targetId;

        return true;
    }

    private static void ApplyDoublePoints(ActiveBoosterEffect effect, List<QuestionScoreResult> results)
    {
        var result = results.FirstOrDefault(r => r.PlayerId == effect.ActivatorPlayerId);
        if (result != null && result.IsCorrect)
        {
            // Double the base points
            result.Points *= 2;
        }
    }

    private static void ApplyPositionSwitch(QuizGameState state, ActiveBoosterEffect effect, List<QuestionScoreResult> results)
    {
        if (!effect.TargetPlayerId.HasValue)
            return;

        var activatorResult = results.FirstOrDefault(r => r.PlayerId == effect.ActivatorPlayerId);
        var targetResult = results.FirstOrDefault(r => r.PlayerId == effect.TargetPlayerId.Value);

        if (activatorResult == null || targetResult == null)
            return;

        // Only swap if activator was wrong and target was correct
        if (!activatorResult.IsCorrect && targetResult.IsCorrect)
        {
            var tempPoints = activatorResult.Points;
            var tempBonus = activatorResult.BonusPoints;
            var tempRank = activatorResult.Rank;

            activatorResult.Points = targetResult.Points;
            activatorResult.BonusPoints = targetResult.BonusPoints;
            activatorResult.Rank = targetResult.Rank;
            activatorResult.IsCorrect = true; // They now "get" the points

            targetResult.Points = tempPoints;
            targetResult.BonusPoints = tempBonus;
            targetResult.Rank = tempRank;
            targetResult.IsCorrect = false; // Their points are "stolen"
        }
    }

    private static void ApplyBackToZero(QuizGameState state, ActiveBoosterEffect effect)
    {
        if (!effect.TargetPlayerId.HasValue)
            return;

        // Reset target's round points
        if (state.RoundPoints.TryGetValue(effect.TargetPlayerId.Value, out var roundPoints))
        {
            var targetPlayer = state.Scoreboard.FirstOrDefault(p => p.PlayerId == effect.TargetPlayerId.Value);
            if (targetPlayer != null)
            {
                targetPlayer.Score -= roundPoints;
                state.RoundPoints[effect.TargetPlayerId.Value] = 0;
            }
        }
    }

    #endregion
}
