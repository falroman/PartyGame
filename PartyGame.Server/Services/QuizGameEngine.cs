using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;

namespace PartyGame.Server.Services;

/// <summary>
/// Engine for quiz game logic. Pure functions for state transitions.
/// </summary>
public class QuizGameEngine : IQuizGameEngine
{
    private readonly IQuizQuestionBank _questionBank;

    public QuizGameEngine(IQuizQuestionBank questionBank)
    {
        _questionBank = questionBank;
    }

    /// <inheritdoc />
    public QuizGameState InitializeGame(Room room, string locale, int totalQuestions = 10)
    {
        ArgumentNullException.ThrowIfNull(room);

        var state = new QuizGameState
        {
            RoomCode = room.Code,
            Locale = locale,
            Phase = QuizPhase.CategorySelection,
            QuestionNumber = 0,
            TotalQuestions = totalQuestions,
            RoundNumber = 0,
            Scoreboard = room.Players.Values
                .Select((p, idx) => new PlayerScoreState
                {
                    PlayerId = p.PlayerId,
                    DisplayName = p.DisplayName,
                    Score = 0,
                    Position = idx + 1
                })
                .ToList()
        };

        // Initialize answers dictionary for all players
        foreach (var player in room.Players.Keys)
        {
            state.Answers[player] = null;
        }

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartNewRound(QuizGameState state, int categorySelectionDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Complete current round if exists
        if (state.CurrentRound != null && !state.CurrentRound.IsCompleted)
        {
            state.CurrentRound.IsCompleted = true;
            state.CompletedRounds.Add(state.CurrentRound);
        }

        // Select round leader
        var roundLeaderId = SelectRoundLeader(state);

        // Create new round
        state.RoundNumber++;
        state.CurrentRound = GameRound.Create(roundLeaderId);
        state.PreviousRoundLeaders.Add(roundLeaderId);

        // Get available categories (excluding used ones)
        var availableCategories = _questionBank.GetRandomCategories(
            state.Locale,
            count: 3,
            excludeCategories: state.UsedCategories);

        state.AvailableCategories = availableCategories.ToList();
        state.Phase = QuizPhase.CategorySelection;
        state.PhaseEndsUtc = currentTime.AddSeconds(categorySelectionDurationSeconds);

        return state;
    }

    /// <inheritdoc />
    public Guid SelectRoundLeader(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Get the last round leader (to avoid picking same player twice in a row)
        var lastRoundLeader = state.PreviousRoundLeaders.LastOrDefault();

        // Find player(s) with lowest score
        var minScore = state.Scoreboard.Min(p => p.Score);
        var lowestScorePlayers = state.Scoreboard
            .Where(p => p.Score == minScore)
            .ToList();

        // If there's only one player with lowest score
        if (lowestScorePlayers.Count == 1)
        {
            var candidate = lowestScorePlayers[0].PlayerId;
            
            // If this player was the last leader, pick the next lowest scorer
            if (candidate == lastRoundLeader && state.Scoreboard.Count > 1)
            {
                var nextLowestScore = state.Scoreboard
                    .Where(p => p.PlayerId != lastRoundLeader)
                    .Min(p => p.Score);
                    
                return state.Scoreboard
                    .Where(p => p.Score == nextLowestScore && p.PlayerId != lastRoundLeader)
                    .First().PlayerId;
            }
            
            return candidate;
        }

        // Multiple players tied - pick first one that wasn't the last leader
        var nonLastLeader = lowestScorePlayers
            .FirstOrDefault(p => p.PlayerId != lastRoundLeader);

        if (nonLastLeader != null)
        {
            return nonLastLeader.PlayerId;
        }

        // Fallback: just pick first player with lowest score
        return lowestScorePlayers[0].PlayerId;
    }

    /// <inheritdoc />
    public QuizGameState SetRoundCategory(QuizGameState state, string category)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (state.CurrentRound == null)
        {
            throw new InvalidOperationException("No active round to set category for.");
        }

        state.CurrentRound.Category = category;
        state.UsedCategories.Add(category);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState? StartNewQuestion(QuizGameState state, int questionDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Determine category filter
        string? categoryFilter = state.CurrentRound?.Category;

        // Get a random question that hasn't been used
        var question = _questionBank.GetRandom(
            locale: state.Locale,
            category: categoryFilter,
            excludeIds: state.UsedQuestionIds);

        if (question == null)
        {
            // No more questions available
            return null;
        }

        // Clear previous answers
        foreach (var key in state.Answers.Keys.ToList())
        {
            state.Answers[key] = null;
        }

        // Update state with new question
        state.QuestionNumber++;
        state.QuestionId = question.Id;
        state.QuestionText = question.Question;
        state.Options = question.Options
            .Select(o => new QuizOptionState { Key = o.Key, Text = o.Text })
            .ToList();
        state.CorrectOptionKey = question.CorrectOptionKey;
        state.Explanation = question.Explanation;
        state.Phase = QuizPhase.Question;
        state.PhaseEndsUtc = currentTime.AddSeconds(questionDurationSeconds);
        state.UsedQuestionIds.Add(question.Id);

        // Increment question index in current round
        if (state.CurrentRound != null)
        {
            state.CurrentRound.CurrentQuestionIndex++;
        }

        // Reset answer status in scoreboard
        foreach (var player in state.Scoreboard)
        {
            player.AnsweredCorrectly = null;
            player.SelectedOption = null;
        }

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.Answering;
        state.PhaseEndsUtc = currentTime.AddSeconds(answeringDurationSeconds);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState SubmitAnswer(QuizGameState state, Guid playerId, string optionKey)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionKey);

        // Check if player exists in the game
        if (!state.Answers.ContainsKey(playerId))
        {
            return state; // Player not in game
        }

        // Idempotent: only record first answer
        if (state.Answers[playerId] != null)
        {
            return state; // Already answered
        }

        // Validate option key
        if (!IsValidOptionKey(state, optionKey))
        {
            return state; // Invalid option, ignore
        }

        // Record the answer
        state.Answers[playerId] = optionKey;

        return state;
    }

    /// <inheritdoc />
    public QuizGameState RevealAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.Reveal;
        state.PhaseEndsUtc = currentTime.AddSeconds(revealDurationSeconds);

        // Calculate scores and update scoreboard
        foreach (var player in state.Scoreboard)
        {
            if (state.Answers.TryGetValue(player.PlayerId, out var answer))
            {
                player.SelectedOption = answer;
                player.AnsweredCorrectly = answer != null && 
                    answer.Equals(state.CorrectOptionKey, StringComparison.OrdinalIgnoreCase);

                if (player.AnsweredCorrectly == true)
                {
                    player.Score += state.PointsPerCorrectAnswer;
                }
            }
        }

        // Update positions based on score
        UpdatePositions(state.Scoreboard);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState ShowScoreboard(QuizGameState state, int scoreboardDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.Scoreboard;
        state.PhaseEndsUtc = currentTime.AddSeconds(scoreboardDurationSeconds);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState FinishGame(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Complete current round if exists
        if (state.CurrentRound != null && !state.CurrentRound.IsCompleted)
        {
            state.CurrentRound.IsCompleted = true;
            state.CompletedRounds.Add(state.CurrentRound);
        }

        state.Phase = QuizPhase.Finished;
        UpdatePositions(state.Scoreboard);

        return state;
    }

    /// <inheritdoc />
    public bool AllPlayersAnswered(QuizGameState state, IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(state);

        return playerIds.All(id => 
            state.Answers.TryGetValue(id, out var answer) && answer != null);
    }

    /// <inheritdoc />
    public bool HasMoreQuestionsInRound(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.CurrentRound == null)
        {
            return false;
        }

        return state.CurrentRound.CurrentQuestionIndex < GameRound.QuestionsPerRound;
    }

    /// <inheritdoc />
    public bool HasMoreQuestions(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.QuestionNumber < state.TotalQuestions;
    }

    /// <inheritdoc />
    public bool IsValidOptionKey(QuizGameState state, string optionKey)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(optionKey))
            return false;

        return state.Options.Any(o => 
            o.Key.Equals(optionKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool IsValidCategory(QuizGameState state, string category)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(category))
            return false;

        return state.AvailableCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    private static void UpdatePositions(List<PlayerScoreState> scoreboard)
    {
        var sorted = scoreboard
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.DisplayName)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Position = i + 1;
        }
    }
}
