using PartyGame.Core.Enums;

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
    string QuestionId,
    string QuestionText,
    IReadOnlyList<QuizOptionDto> Options,
    string? CorrectOptionKey,
    string? Explanation,
    int RemainingSeconds,
    IReadOnlyList<PlayerAnswerStatusDto> AnswerStatuses,
    IReadOnlyList<PlayerScoreDto> Scoreboard
);

/// <summary>
/// DTO for a quiz answer option.
/// </summary>
public record QuizOptionDto(
    string Key,
    string Text
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
    string? SelectedOption
);
