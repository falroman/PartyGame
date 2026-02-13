using PartyGame.Core.Enums;
using PartyGame.Core.Models;

namespace PartyGame.Server.DTOs;

/// <summary>
/// DTO for quiz game state sent to clients.
/// This is a "safe" version that hides sensitive data based on phase.
/// </summary>
public record QuizGameStateDto(
    QuizPhase Phase,
    int QuestionNumber,
    int TotalQuestions,
    int RoundNumber,
    int QuestionsInRound,
    int CurrentQuestionInRound,
    string? CurrentCategory,
    Guid? RoundLeaderPlayerId,
    IReadOnlyList<string>? AvailableCategories,
    RoundType? RoundType,
    string QuestionId,
    string QuestionText,
    IReadOnlyList<QuizOptionDto> Options,
    IReadOnlyList<PlayerOptionDto>? PlayerOptions,  // For RankingStars - players to vote for
    string? CorrectOptionKey,
    string? Explanation,
    IReadOnlyList<Guid>? RankingWinnerIds,  // For RankingStars reveal
    Dictionary<Guid, int>? RankingVoteCounts,  // For RankingStars reveal
    int RemainingSeconds,
    DateTime PhaseEndsUtc,  // UTC timestamp for client-side smooth timer
    IReadOnlyList<PlayerAnswerStatusDto> AnswerStatuses,
    IReadOnlyList<PlayerScoreDto> Scoreboard,
    // Booster information
    IReadOnlyList<PlayerBoosterStateDto>? PlayerBoosters,
    IReadOnlyList<ActiveBoosterEffectDto>? ActiveEffects,
    PlayerAnsweringEffectsDto? MyAnsweringEffects  // Player-specific effects (only for the requesting player)
);

/// <summary>
/// DTO for a quiz answer option.
/// </summary>
public record QuizOptionDto(
    string Key,
    string Text
);

/// <summary>
/// DTO for a player option in RankingStars voting.
/// </summary>
public record PlayerOptionDto(
    Guid PlayerId,
    string DisplayName
);

/// <summary>
/// DTO showing whether a player has answered (without revealing their choice).
/// Used during Answering phase.
/// </summary>
public record PlayerAnswerStatusDto(
    Guid PlayerId,
    string DisplayName,
    bool HasAnswered
);

/// <summary>
/// DTO for player score on scoreboard.
/// </summary>
public record PlayerScoreDto(
    Guid PlayerId,
    string DisplayName,
    int Score,
    int Position,
    bool? AnsweredCorrectly,
    string? SelectedOption,
    int PointsEarned = 0,
    bool GotSpeedBonus = false,
    bool IsRankingStar = false,
    int RankingVotesReceived = 0,
    int Rank = 0,  // Rank for current question (1 = fastest correct)
    string? AvatarPresetId = null,
    string? AvatarUrl = null,
    AvatarKind AvatarKind = AvatarKind.Preset
);

/// <summary>
/// DTO for a player's booster state.
/// </summary>
public record PlayerBoosterStateDto(
    Guid PlayerId,
    BoosterType BoosterType,
    string BoosterName,
    string BoosterDescription,
    bool IsUsed,
    bool RequiresTarget,
    string[] ValidPhases
);

/// <summary>
/// DTO for an active booster effect.
/// </summary>
public record ActiveBoosterEffectDto(
    BoosterType BoosterType,
    Guid ActivatorPlayerId,
    string ActivatorName,
    Guid? TargetPlayerId,
    string? TargetName,
    int QuestionNumber
);

/// <summary>
/// DTO for player-specific answering effects.
/// Only sent to the specific player it affects.
/// </summary>
public record PlayerAnsweringEffectsDto(
    bool IsNoped,
    IReadOnlyList<string>? RemovedOptions,
    IReadOnlyList<string>? ShuffledOptionOrder,
    DateTime? ExtendedDeadline,
    Guid? MirrorTargetId,
    bool CanChangeAnswer
);

/// <summary>
/// Event DTO for booster activation broadcasts.
/// </summary>
public record BoosterActivatedEventDto(
    BoosterType BoosterType,
    string BoosterName,
    Guid ActivatorPlayerId,
    string ActivatorName,
    Guid? TargetPlayerId,
    string? TargetName,
    bool WasBlockedByShield,
    Guid? ShieldBlockerPlayerId,
    string? ShieldBlockerName
);
