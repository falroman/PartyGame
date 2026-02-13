using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Dictionary;
using PartyGame.Core.Models.Ranking;
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

    #region Game Initialization

    /// <inheritdoc />
    public QuizGameState InitializeGame(Room room, string locale, IEnumerable<RoundType> plannedRounds)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(plannedRounds);

        var roundsList = plannedRounds.ToList();
        
        // Ensure DictionaryGame is last
        if (roundsList.Count == 0 || roundsList.Last() != RoundType.DictionaryGame)
        {
            roundsList.Add(RoundType.DictionaryGame);
        }

        // Calculate total questions (excluding dictionary round)
        var quizRoundCount = roundsList.Count(r => r == RoundType.CategoryQuiz);
        var rankingRoundCount = roundsList.Count(r => r == RoundType.RankingStars);
        var totalQuestions = (quizRoundCount * GameRound.QuestionsPerRound) + 
                            (rankingRoundCount * QuizGameState.RankingPromptsPerRound);

        var state = new QuizGameState
        {
            RoomCode = room.Code,
            Locale = locale,
            Phase = QuizPhase.CategorySelection,
            QuestionNumber = 0,
            TotalQuestions = totalQuestions,
            RoundNumber = 0,
            PlannedRounds = roundsList,
            PlannedRoundIndex = -1,
            Scoreboard = room.Players.Values
                .Select((p, idx) => new PlayerScoreState
                {
                    PlayerId = p.PlayerId,
                    DisplayName = p.DisplayName,
                    Score = 0,
                    Position = idx + 1,
                    // Copy avatar information from player
                    AvatarPresetId = p.AvatarPresetId,
                    AvatarUrl = p.AvatarUrl,
                    AvatarKind = p.AvatarKind
                })
                .ToList()
        };

        // Initialize answers dictionaries for all players
        foreach (var player in room.Players.Keys)
        {
            state.Answers[player] = null;
            state.DictionaryAnswers[player] = null;
            state.RankingVotes[player] = null;
        }

        return state;
    }

    /// <inheritdoc />
    public QuizGameState InitializeGame(Room room, string locale, int totalQuestions = 10)
    {
        // Default plan: 2x CategoryQuiz, 1x RankingStars, 1x DictionaryGame (always last)
        var defaultPlan = new List<RoundType>
        {
            RoundType.CategoryQuiz,
            RoundType.CategoryQuiz,
            RoundType.RankingStars,
            RoundType.DictionaryGame
        };

        return InitializeGame(room, locale, defaultPlan);
    }

    /// <inheritdoc />
    public RoundType? GetNextRoundType(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var nextIndex = state.PlannedRoundIndex + 1;
        if (nextIndex >= state.PlannedRounds.Count)
            return null;

        return state.PlannedRounds[nextIndex];
    }

    /// <inheritdoc />
    public bool HasMorePlannedRounds(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PlannedRoundIndex + 1 < state.PlannedRounds.Count;
    }

    #endregion

    #region Category Quiz Methods

    /// <inheritdoc />
    public QuizGameState StartNewRound(QuizGameState state, int categorySelectionDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Complete current round if exists
        CompleteCurrentRound(state);

        // Advance planned round index
        state.PlannedRoundIndex++;

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

        var lastRoundLeader = state.PreviousRoundLeaders.LastOrDefault();
        var minScore = state.Scoreboard.Min(p => p.Score);
        var lowestScorePlayers = state.Scoreboard
            .Where(p => p.Score == minScore)
            .ToList();

        if (lowestScorePlayers.Count == 1)
        {
            var candidate = lowestScorePlayers[0].PlayerId;
            
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

        var nonLastLeader = lowestScorePlayers
            .FirstOrDefault(p => p.PlayerId != lastRoundLeader);

        return nonLastLeader?.PlayerId ?? lowestScorePlayers[0].PlayerId;
    }

    /// <inheritdoc />
    public QuizGameState SetRoundCategory(QuizGameState state, string category)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (state.CurrentRound == null)
            throw new InvalidOperationException("No active round to set category for.");

        state.CurrentRound.Category = category;
        state.UsedCategories.Add(category);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState? StartNewQuestion(QuizGameState state, int questionDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? categoryFilter = state.CurrentRound?.Category;

        var question = _questionBank.GetRandom(
            locale: state.Locale,
            category: categoryFilter,
            excludeIds: state.UsedQuestionIds);

        if (question == null)
            return null;

        foreach (var key in state.Answers.Keys.ToList())
            state.Answers[key] = null;

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

        if (state.CurrentRound != null)
            state.CurrentRound.CurrentQuestionIndex++;

        ResetScoreboardStatus(state);

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

        if (!state.Answers.ContainsKey(playerId) || state.Answers[playerId] != null)
            return state;

        if (!IsValidOptionKey(state, optionKey))
            return state;

        state.Answers[playerId] = optionKey;
        return state;
    }

    /// <inheritdoc />
    public QuizGameState RevealAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.Reveal;
        state.PhaseEndsUtc = currentTime.AddSeconds(revealDurationSeconds);

        foreach (var player in state.Scoreboard)
        {
            player.PointsEarned = 0;

            if (state.Answers.TryGetValue(player.PlayerId, out var answer))
            {
                player.SelectedOption = answer;
                player.AnsweredCorrectly = answer != null && 
                    answer.Equals(state.CorrectOptionKey, StringComparison.OrdinalIgnoreCase);

                if (player.AnsweredCorrectly == true)
                {
                    player.PointsEarned = state.PointsPerCorrectAnswer;
                    player.Score += state.PointsPerCorrectAnswer;
                }
            }
        }

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
        CompleteCurrentRound(state);
        state.Phase = QuizPhase.Finished;
        UpdatePositions(state.Scoreboard);
        return state;
    }

    /// <inheritdoc />
    public bool AllPlayersAnswered(QuizGameState state, IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        return playerIds.All(id => state.Answers.TryGetValue(id, out var answer) && answer != null);
    }

    /// <inheritdoc />
    public bool HasMoreQuestionsInRound(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CurrentRound == null) return false;
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
        if (string.IsNullOrWhiteSpace(optionKey)) return false;
        return state.Options.Any(o => o.Key.Equals(optionKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool IsValidCategory(QuizGameState state, string category)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(category)) return false;
        return state.AvailableCategories.Contains(category, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool ShouldStartDictionaryRound(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Use the new planned rounds system
        var nextRound = GetNextRoundType(state);
        return nextRound == RoundType.DictionaryGame;
    }

    #endregion

    #region Ranking Stars Methods

    /// <inheritdoc />
    public QuizGameState StartRankingRound(QuizGameState state, int promptDisplayDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        CompleteCurrentRound(state);

        // Advance planned round index
        state.PlannedRoundIndex++;

        state.RoundNumber++;
        state.CurrentRound = GameRound.CreateRankingRound();
        state.RankingPromptIndex = 0;

        // Reset ranking votes
        foreach (var key in state.RankingVotes.Keys.ToList())
            state.RankingVotes[key] = null;
        state.RankingVoteTimes.Clear();
        state.RankingResult = null;

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartRankingPrompt(QuizGameState state, RankingPrompt prompt, int promptDisplayDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(prompt);

        state.RankingPromptId = prompt.Id;
        state.RankingPromptText = prompt.Prompt;
        state.RankingPromptIndex++;
        state.QuestionNumber++;

        if (state.CurrentRound != null)
            state.CurrentRound.CurrentQuestionIndex++;

        state.UsedRankingPromptIds.Add(prompt.Id);

        // Reset votes for this prompt
        foreach (var key in state.RankingVotes.Keys.ToList())
            state.RankingVotes[key] = null;
        state.RankingVoteTimes.Clear();
        state.RankingResult = null;

        ResetScoreboardStatus(state);

        state.Phase = QuizPhase.RankingPrompt;
        state.PhaseEndsUtc = currentTime.AddSeconds(promptDisplayDurationSeconds);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartRankingVotingPhase(QuizGameState state, int votingDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Phase = QuizPhase.RankingVoting;
        state.PhaseEndsUtc = currentTime.AddSeconds(votingDurationSeconds);
        return state;
    }

    /// <inheritdoc />
    public QuizGameState SubmitRankingVote(QuizGameState state, Guid voterPlayerId, Guid votedForPlayerId, DateTime voteTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Check if voter exists
        if (!state.RankingVotes.ContainsKey(voterPlayerId))
            return state;

        // Idempotent: only record first vote
        if (state.RankingVotes[voterPlayerId] != null)
            return state;

        // Validate vote
        if (!IsValidRankingVote(state, voterPlayerId, votedForPlayerId))
            return state;

        state.RankingVotes[voterPlayerId] = votedForPlayerId;
        state.RankingVoteTimes[voterPlayerId] = voteTime;

        return state;
    }

    /// <inheritdoc />
    public QuizGameState RevealRankingVotes(QuizGameState state, int revealDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.RankingReveal;
        state.PhaseEndsUtc = currentTime.AddSeconds(revealDurationSeconds);

        // Calculate result
        var result = CalculateRankingResult(state);
        state.RankingResult = result;

        // Calculate median score for catch-up bonus
        var sortedScores = state.Scoreboard.OrderBy(p => p.Score).ToList();
        var medianIndex = sortedScores.Count / 2;
        var medianScore = sortedScores.Count > 0 ? sortedScores[medianIndex].Score : 0;

        // Award points
        foreach (var player in state.Scoreboard)
        {
            player.PointsEarned = 0;
            player.IsRankingStar = false;
            player.RankingVotesReceived = result.VoteCounts.TryGetValue(player.PlayerId, out var votes) ? votes : 0;

            // Check if this player is a star (winner)
            if (result.WinnerPlayerIds.Contains(player.PlayerId))
            {
                player.IsRankingStar = true;
                player.PointsEarned += QuizGameState.RankingStarPoints;
                player.AnsweredCorrectly = true; // Mark as "correct" for UI
            }

            // Check if this player voted for the winner
            if (result.CorrectVoters.Contains(player.PlayerId))
            {
                player.PointsEarned += QuizGameState.RankingCorrectVotePoints;
                if (!player.IsRankingStar) // Don't mark stars as "answered correctly" twice
                    player.AnsweredCorrectly = true;

                // Catch-up bonus for players in bottom half
                if (player.Score <= medianScore && sortedScores.Count > 1)
                {
                    player.PointsEarned += QuizGameState.RankingCatchUpBonusPoints;
                }
            }
            else if (!player.IsRankingStar)
            {
                player.AnsweredCorrectly = false;
            }

            // Record what they voted for
            if (state.RankingVotes.TryGetValue(player.PlayerId, out var votedFor) && votedFor.HasValue)
            {
                player.SelectedOption = votedFor.Value.ToString();
            }

            player.Score += player.PointsEarned;
        }

        UpdatePositions(state.Scoreboard);
        return state;
    }

    /// <inheritdoc />
    public bool HasMoreRankingPrompts(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.RankingPromptIndex < QuizGameState.RankingPromptsPerRound;
    }

    /// <inheritdoc />
    public bool AllRankingVoted(QuizGameState state, IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        return playerIds.All(id => state.RankingVotes.TryGetValue(id, out var vote) && vote != null);
    }

    /// <inheritdoc />
    public bool IsValidRankingVote(QuizGameState state, Guid voterPlayerId, Guid votedForPlayerId)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Voter must exist
        if (!state.Scoreboard.Any(p => p.PlayerId == voterPlayerId))
            return false;

        // Target must exist
        if (!state.Scoreboard.Any(p => p.PlayerId == votedForPlayerId))
            return false;

        // Cannot vote for self
        if (voterPlayerId == votedForPlayerId)
            return false;

        return true;
    }

    /// <inheritdoc />
    public RankingVoteResult CalculateRankingResult(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var result = new RankingVoteResult();

        // Count votes for each player
        foreach (var (voter, votedFor) in state.RankingVotes)
        {
            if (votedFor.HasValue)
            {
                if (!result.VoteCounts.ContainsKey(votedFor.Value))
                    result.VoteCounts[votedFor.Value] = 0;
                result.VoteCounts[votedFor.Value]++;
            }
        }

        // Find max votes
        result.MaxVotes = result.VoteCounts.Count > 0 ? result.VoteCounts.Values.Max() : 0;

        // Find winner(s) - could be multiple in case of tie
        if (result.MaxVotes > 0)
        {
            result.WinnerPlayerIds = result.VoteCounts
                .Where(kv => kv.Value == result.MaxVotes)
                .Select(kv => kv.Key)
                .ToList();
        }

        // Find voters who voted for a winner
        foreach (var (voter, votedFor) in state.RankingVotes)
        {
            if (votedFor.HasValue && result.WinnerPlayerIds.Contains(votedFor.Value))
            {
                result.CorrectVoters.Add(voter);
            }
        }

        return result;
    }

    #endregion

    #region Dictionary Game Methods

    /// <inheritdoc />
    public QuizGameState StartDictionaryRound(QuizGameState state, int wordDisplayDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        CompleteCurrentRound(state);

        // Advance planned round index
        state.PlannedRoundIndex++;

        state.RoundNumber++;
        state.CurrentRound = GameRound.CreateDictionaryRound();
        state.DictionaryWordIndex = 0;

        foreach (var key in state.DictionaryAnswers.Keys.ToList())
            state.DictionaryAnswers[key] = null;
        state.DictionaryAnswerTimes.Clear();

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartDictionaryWord(QuizGameState state, DictionaryQuestion question, int wordDisplayDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(question);

        state.DictionaryQuestion = question;
        state.DictionaryWordIndex++;
        state.QuestionNumber++;

        if (state.CurrentRound != null)
            state.CurrentRound.CurrentQuestionIndex++;

        state.UsedDictionaryWords.Add(question.Word);

        foreach (var key in state.DictionaryAnswers.Keys.ToList())
            state.DictionaryAnswers[key] = null;
        state.DictionaryAnswerTimes.Clear();

        ResetScoreboardStatus(state);

        state.Phase = QuizPhase.DictionaryWord;
        state.PhaseEndsUtc = currentTime.AddSeconds(wordDisplayDurationSeconds);

        return state;
    }

    /// <inheritdoc />
    public QuizGameState StartDictionaryAnsweringPhase(QuizGameState state, int answeringDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Phase = QuizPhase.DictionaryAnswering;
        state.PhaseEndsUtc = currentTime.AddSeconds(answeringDurationSeconds);
        return state;
    }

    /// <inheritdoc />
    public QuizGameState SubmitDictionaryAnswer(QuizGameState state, Guid playerId, int optionIndex, DateTime answerTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.DictionaryAnswers.ContainsKey(playerId) || state.DictionaryAnswers[playerId] != null)
            return state;

        if (!IsValidDictionaryOption(optionIndex))
            return state;

        state.DictionaryAnswers[playerId] = optionIndex;
        state.DictionaryAnswerTimes[playerId] = answerTime;

        return state;
    }

    /// <inheritdoc />
    public QuizGameState RevealDictionaryAnswer(QuizGameState state, int revealDurationSeconds, DateTime currentTime)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Phase = QuizPhase.Reveal;
        state.PhaseEndsUtc = currentTime.AddSeconds(revealDurationSeconds);

        if (state.DictionaryQuestion == null)
            return state;

        var correctIndex = state.DictionaryQuestion.CorrectIndex;

        // Find fastest correct answer
        DateTime? fastestCorrectTime = null;
        Guid? fastestPlayerId = null;

        foreach (var (playerId, answerTime) in state.DictionaryAnswerTimes)
        {
            if (state.DictionaryAnswers.TryGetValue(playerId, out var answer) && answer == correctIndex)
            {
                if (fastestCorrectTime == null || answerTime < fastestCorrectTime)
                {
                    fastestCorrectTime = answerTime;
                    fastestPlayerId = playerId;
                }
            }
        }

        var sortedScores = state.Scoreboard.OrderBy(p => p.Score).ToList();
        var medianIndex = sortedScores.Count / 2;
        var medianScore = sortedScores.Count > 0 ? sortedScores[medianIndex].Score : 0;

        foreach (var player in state.Scoreboard)
        {
            player.PointsEarned = 0;
            player.GotSpeedBonus = false;

            if (state.DictionaryAnswers.TryGetValue(player.PlayerId, out var answer))
            {
                player.SelectedOption = answer?.ToString();
                player.AnsweredCorrectly = answer == correctIndex;

                if (player.AnsweredCorrectly == true)
                {
                    player.PointsEarned = QuizGameState.DictionaryCorrectPoints;

                    if (player.PlayerId == fastestPlayerId)
                    {
                        player.PointsEarned += QuizGameState.DictionarySpeedBonusPoints;
                        player.GotSpeedBonus = true;
                    }

                    if (player.Score <= medianScore && sortedScores.Count > 1)
                    {
                        player.PointsEarned += QuizGameState.DictionaryCatchUpBonusPoints;
                    }

                    player.Score += player.PointsEarned;
                }
            }
            else
            {
                player.SelectedOption = null;
                player.AnsweredCorrectly = false;
            }
        }

        UpdatePositions(state.Scoreboard);
        return state;
    }

    /// <inheritdoc />
    public bool HasMoreDictionaryWords(QuizGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.DictionaryWordIndex < QuizGameState.DictionaryWordsPerRound;
    }

    /// <inheritdoc />
    public bool AllDictionaryAnswered(QuizGameState state, IEnumerable<Guid> playerIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        return playerIds.All(id => state.DictionaryAnswers.TryGetValue(id, out var answer) && answer != null);
    }

    /// <inheritdoc />
    public bool IsValidDictionaryOption(int optionIndex)
    {
        return optionIndex >= 0 && optionIndex <= 3;
    }

    #endregion

    #region Helper Methods

    private static void CompleteCurrentRound(QuizGameState state)
    {
        if (state.CurrentRound != null && !state.CurrentRound.IsCompleted)
        {
            state.CurrentRound.IsCompleted = true;
            state.CompletedRounds.Add(state.CurrentRound);
        }
    }

    private static void ResetScoreboardStatus(QuizGameState state)
    {
        foreach (var player in state.Scoreboard)
        {
            player.AnsweredCorrectly = null;
            player.SelectedOption = null;
            player.PointsEarned = 0;
            player.GotSpeedBonus = false;
            player.IsRankingStar = false;
            player.RankingVotesReceived = 0;
        }
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

    #endregion
}
