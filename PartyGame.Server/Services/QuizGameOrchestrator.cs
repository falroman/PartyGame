using Microsoft.AspNetCore.SignalR;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;
using PartyGame.Core.Models.Scoring;
using PartyGame.Server.DTOs;
using PartyGame.Server.Hubs;
using System.Collections.Concurrent;

namespace PartyGame.Server.Services;

/// <summary>
/// Orchestrates quiz game flow including timers and broadcasting.
/// </summary>
public interface IQuizGameOrchestrator
{
    Task<(bool Success, ErrorDto? Error)> StartGameAsync(Room room);
    Task<(bool Success, ErrorDto? Error)> SelectCategoryAsync(string roomCode, Guid playerId, string category);
    Task<(bool Success, ErrorDto? Error)> SubmitAnswerAsync(string roomCode, Guid playerId, string optionKey);
    Task<(bool Success, ErrorDto? Error)> SubmitDictionaryAnswerAsync(string roomCode, Guid playerId, int optionIndex);
    Task<(bool Success, ErrorDto? Error)> SubmitRankingVoteAsync(string roomCode, Guid voterPlayerId, Guid votedForPlayerId);
    Task<(bool Success, ErrorDto? Error)> NextQuestionAsync(string roomCode, string connectionId);
    QuizGameState? GetState(string roomCode);
    void StopGame(string roomCode);
}

/// <summary>
/// Orchestrates quiz game flow including timers and broadcasting.
/// </summary>
public class QuizGameOrchestrator : IQuizGameOrchestrator
{
    private readonly IQuizGameEngine _engine;
    private readonly IRoomStore _roomStore;
    private readonly IClock _clock;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<QuizGameOrchestrator> _logger;
    private readonly IDictionaryQuestionProvider _dictionaryProvider;
    private readonly IRankingStarsPromptProvider _rankingProvider;
    private readonly IBoosterService _boosterService;
    private readonly IScoringService _scoringService;

    private readonly ConcurrentDictionary<string, QuizGameState> _gameStates = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _roomTimers = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _roomLocks = new();

    // Category Quiz timing
    private const int CategorySelectionSeconds = 30;
    private const int QuestionDisplaySeconds = 3;
    private const int AnsweringSeconds = 15;
    private const int RevealSeconds = 5;
    private const int ScoreboardSeconds = 5;

    // Dictionary game timing
    private const int DictionaryWordDisplaySeconds = 3;
    private const int DictionaryAnsweringSeconds = 12;
    private const int DictionaryRevealSeconds = 6;

    // Ranking Stars timing
    private const int RankingPromptDisplaySeconds = 2;
    private const int RankingVotingSeconds = 15;
    private const int RankingRevealSeconds = 6;

    public QuizGameOrchestrator(
        IQuizGameEngine engine,
        IRoomStore roomStore,
        IClock clock,
        IHubContext<GameHub> hubContext,
        ILogger<QuizGameOrchestrator> logger,
        IDictionaryQuestionProvider dictionaryProvider,
        IRankingStarsPromptProvider rankingProvider,
        IBoosterService boosterService,
        IScoringService scoringService)
    {
        _engine = engine;
        _roomStore = roomStore;
        _clock = clock;
        _hubContext = hubContext;
        _logger = logger;
        _dictionaryProvider = dictionaryProvider;
        _rankingProvider = rankingProvider;
        _boosterService = boosterService;
        _scoringService = scoringService;
    }

    public async Task<(bool Success, ErrorDto? Error)> StartGameAsync(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        var roomCode = room.Code.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(roomCode, async () =>
        {
            // Default round plan: 2x CategoryQuiz, 1x RankingStars, 1x DictionaryGame
            var plannedRounds = new List<RoundType>
            {
                RoundType.CategoryQuiz,
                RoundType.CategoryQuiz,
                RoundType.RankingStars,
                RoundType.DictionaryGame  // Always last
            };

            var state = _engine.InitializeGame(room, "nl-BE", plannedRounds);
            _gameStates[roomCode] = state;

            // Assign boosters to all players at game start
            _boosterService.AssignBoostersAtGameStart(state);

            _logger.LogInformation("Quiz game initialized for room {RoomCode} with {RoundCount} planned rounds and boosters assigned", 
                roomCode, plannedRounds.Count);

            await StartNextPlannedRoundAsync(roomCode);

            return (true, (ErrorDto?)null);
        });
    }

    public async Task<(bool Success, ErrorDto? Error)> SelectCategoryAsync(string roomCode, Guid playerId, string category)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(normalizedCode, async () =>
        {
            if (!_gameStates.TryGetValue(normalizedCode, out var state))
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));

            if (state.Phase != QuizPhase.CategorySelection)
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Category selection is not active."));

            if (state.CurrentRound?.RoundLeaderPlayerId != playerId)
                return (false, new ErrorDto(ErrorCodes.NotRoundLeader, "Only the round leader can select a category."));

            if (!string.IsNullOrEmpty(state.CurrentRound.Category))
                return (false, new ErrorDto(ErrorCodes.RoundAlreadyStarted, "Category has already been selected."));

            if (!_engine.IsValidCategory(state, category))
                return (false, new ErrorDto(ErrorCodes.InvalidCategory, "Invalid category selection."));

            _engine.SetRoundCategory(state, category);
            
            _logger.LogInformation("Round leader {PlayerId} selected category '{Category}' in room {RoomCode}", 
                playerId, category, normalizedCode);

            CancelTimer(normalizedCode);
            await BroadcastStateAsync(normalizedCode, state);

            SchedulePhaseTransition(normalizedCode, 2, async () =>
            {
                await StartNextQuestionAsync(normalizedCode);
            });

            return (true, null);
        });
    }

    public async Task<(bool Success, ErrorDto? Error)> SubmitAnswerAsync(string roomCode, Guid playerId, string optionKey)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(normalizedCode, async () =>
        {
            if (!_gameStates.TryGetValue(normalizedCode, out var state))
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));

            if (state.Phase != QuizPhase.Answering)
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Answers are not being accepted at this time."));

            if (!state.Answers.ContainsKey(playerId))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Player is not in this game."));

            if (!_engine.IsValidOptionKey(state, optionKey))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Invalid option."));

            _engine.SubmitAnswer(state, playerId, optionKey);

            _logger.LogInformation("Player {PlayerId} submitted answer {Option} in room {RoomCode}", 
                playerId, optionKey, normalizedCode);

            await BroadcastStateAsync(normalizedCode, state);

            if (await CheckAllAnsweredAndAdvance(normalizedCode, state, 
                ids => _engine.AllPlayersAnswered(state, ids),
                () => AdvanceFromAnsweringAsync(normalizedCode)))
            {
                return (true, null);
            }

            return (true, null);
        });
    }

    public async Task<(bool Success, ErrorDto? Error)> SubmitDictionaryAnswerAsync(string roomCode, Guid playerId, int optionIndex)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(normalizedCode, async () =>
        {
            if (!_gameStates.TryGetValue(normalizedCode, out var state))
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));

            if (state.Phase != QuizPhase.DictionaryAnswering)
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Dictionary answers are not being accepted at this time."));

            if (!state.DictionaryAnswers.ContainsKey(playerId))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Player is not in this game."));

            if (!_engine.IsValidDictionaryOption(optionIndex))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Invalid option index (must be 0-3)."));

            _engine.SubmitDictionaryAnswer(state, playerId, optionIndex, _clock.UtcNow);

            _logger.LogInformation("Player {PlayerId} submitted dictionary answer {Option} in room {RoomCode}", 
                playerId, optionIndex, normalizedCode);

            await BroadcastStateAsync(normalizedCode, state);

            await CheckAllAnsweredAndAdvance(normalizedCode, state,
                ids => _engine.AllDictionaryAnswered(state, ids),
                () => AdvanceFromDictionaryAnsweringAsync(normalizedCode));

            return (true, null);
        });
    }

    public async Task<(bool Success, ErrorDto? Error)> SubmitRankingVoteAsync(string roomCode, Guid voterPlayerId, Guid votedForPlayerId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(normalizedCode, async () =>
        {
            if (!_gameStates.TryGetValue(normalizedCode, out var state))
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));

            if (state.Phase != QuizPhase.RankingVoting)
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Ranking votes are not being accepted at this time."));

            if (!state.RankingVotes.ContainsKey(voterPlayerId))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Player is not in this game."));

            if (!_engine.IsValidRankingVote(state, voterPlayerId, votedForPlayerId))
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Invalid vote (cannot vote for yourself or non-existent player)."));

            _engine.SubmitRankingVote(state, voterPlayerId, votedForPlayerId, _clock.UtcNow);

            _logger.LogInformation("Player {VoterId} voted for {VotedForId} in room {RoomCode}", 
                voterPlayerId, votedForPlayerId, normalizedCode);

            await BroadcastStateAsync(normalizedCode, state);

            await CheckAllAnsweredAndAdvance(normalizedCode, state,
                ids => _engine.AllRankingVoted(state, ids),
                () => AdvanceFromRankingVotingAsync(normalizedCode));

            return (true, null);
        });
    }

    public async Task<(bool Success, ErrorDto? Error)> NextQuestionAsync(string roomCode, string connectionId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        return await ExecuteWithRoomLockAsync(normalizedCode, async () =>
        {
            if (!_gameStates.TryGetValue(normalizedCode, out var state))
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));

            if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
                return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Room not found."));

            if (room.HostConnectionId != connectionId)
                return (false, new ErrorDto(ErrorCodes.NotHost, "Only the host can advance the game."));

            if (state.Phase != QuizPhase.Scoreboard)
                return (false, new ErrorDto(ErrorCodes.InvalidState, "Cannot advance at this time."));

            CancelTimer(normalizedCode);
            await HandleScoreboardCompleteAsync(normalizedCode);

            return (true, null);
        });
    }

    public QuizGameState? GetState(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        return _gameStates.TryGetValue(normalizedCode, out var state) ? state : null;
    }

    public void StopGame(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        CancelTimer(normalizedCode);
        _gameStates.TryRemove(normalizedCode, out _);
        
        // Clean up the semaphore to prevent memory leak
        if (_roomLocks.TryRemove(normalizedCode, out var semaphore))
        {
            semaphore.Dispose();
        }
        
        _logger.LogInformation("Quiz game stopped for room {RoomCode}", normalizedCode);
    }

    #region Round Management

    private async Task StartNextPlannedRoundAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        var nextRoundType = _engine.GetNextRoundType(state);
        
        if (nextRoundType == null)
        {
            _logger.LogInformation("No more planned rounds in room {RoomCode}, finishing game", roomCode);
            _engine.FinishGame(state);
            await BroadcastStateAsync(roomCode, state);
            UpdateRoomStatus(roomCode, RoomStatus.Finished);
            return;
        }

        _logger.LogInformation("Starting next round type {RoundType} in room {RoomCode}", nextRoundType, roomCode);

        switch (nextRoundType.Value)
        {
            case RoundType.CategoryQuiz:
                await StartCategoryQuizRoundAsync(roomCode);
                break;
            case RoundType.RankingStars:
                await StartRankingRoundAsync(roomCode);
                break;
            case RoundType.DictionaryGame:
                await StartDictionaryRoundAsync(roomCode);
                break;
        }
    }

    private async Task StartCategoryQuizRoundAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartNewRound(state, CategorySelectionSeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started CategoryQuiz round {RoundNumber} in room {RoomCode}, leader: {LeaderId}", 
            state.RoundNumber, roomCode, state.CurrentRound?.RoundLeaderPlayerId);

        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, CategorySelectionSeconds, async () =>
        {
            await AutoSelectCategoryAsync(roomCode);
        });
    }

    private async Task AutoSelectCategoryAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        if (state.Phase != QuizPhase.CategorySelection)
            return;

        var category = state.AvailableCategories.FirstOrDefault();
        if (string.IsNullOrEmpty(category))
        {
            _logger.LogWarning("No categories available for auto-select in room {RoomCode}", roomCode);
            return;
        }

        _engine.SetRoundCategory(state, category);
        _logger.LogInformation("Auto-selected category '{Category}' in room {RoomCode}", category, roomCode);

        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, 2, async () =>
        {
            await StartNextQuestionAsync(roomCode);
        });
    }

    #endregion

    #region Category Quiz Flow

    private async Task StartNextQuestionAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        var newState = _engine.StartNewQuestion(state, QuestionDisplaySeconds, _clock.UtcNow);
        
        if (newState == null)
        {
            // No more questions in this round
            await HandleRoundCompleteAsync(roomCode);
            return;
        }

        _gameStates[roomCode] = newState;
        await BroadcastStateAsync(roomCode, newState);

        SchedulePhaseTransition(roomCode, QuestionDisplaySeconds, async () =>
        {
            await TransitionToAnsweringAsync(roomCode);
        });
    }

    private async Task TransitionToAnsweringAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartAnsweringPhase(state, AnsweringSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, AnsweringSeconds, async () =>
        {
            await AdvanceFromAnsweringAsync(roomCode);
        });
    }

    private async Task AdvanceFromAnsweringAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.RevealAnswer(state, RevealSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);
        UpdatePlayerScores(roomCode, state);

        SchedulePhaseTransition(roomCode, RevealSeconds, async () =>
        {
            await HandleRevealCompleteAsync(roomCode);
        });
    }

    /// <summary>
    /// After reveal: go directly to next question unless it's the last question in round.
    /// Scoreboard only shows at end of round (after question 3).
    /// </summary>
    private async Task HandleRevealCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Check if this is the last question in the current round
        var isEndOfRound = !_engine.HasMoreQuestionsInRound(state);

        if (isEndOfRound)
        {
            // Show scoreboard at end of round
            await TransitionToScoreboardAsync(roomCode);
        }
        else
        {
            // Go directly to next question (skip scoreboard mid-round)
            await StartNextQuestionAsync(roomCode);
        }
    }

    private async Task TransitionToScoreboardAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.ShowScoreboard(state, ScoreboardSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, ScoreboardSeconds, async () =>
        {
            await HandleScoreboardCompleteAsync(roomCode);
        });
    }

    private async Task HandleScoreboardCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // After scoreboard, move to next round
        await HandleRoundCompleteAsync(roomCode);
    }

    private async Task HandleRoundCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _logger.LogInformation("Round {RoundNumber} ({RoundType}) complete in room {RoomCode}", 
            state.RoundNumber, state.CurrentRound?.Type, roomCode);

        // Check if there are more rounds
        if (_engine.HasMorePlannedRounds(state))
        {
            await StartNextPlannedRoundAsync(roomCode);
        }
        else
        {
            _logger.LogInformation("All rounds complete in room {RoomCode}, finishing game", roomCode);
            _engine.FinishGame(state);
            await BroadcastStateAsync(roomCode, state);
            UpdateRoomStatus(roomCode, RoomStatus.Finished);
        }
    }

    #endregion

    #region Ranking Stars Flow

    private async Task StartRankingRoundAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartRankingRound(state, RankingPromptDisplaySeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started RankingStars round {RoundNumber} in room {RoomCode}", 
            state.RoundNumber, roomCode);

        await StartNextRankingPromptAsync(roomCode);
    }

    private async Task StartNextRankingPromptAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        var prompt = _rankingProvider.GetRandomPrompt(state.Locale, state.UsedRankingPromptIds);
        
        if (prompt == null)
        {
            _logger.LogWarning("No ranking prompts available in room {RoomCode}", roomCode);
            await HandleRoundCompleteAsync(roomCode);
            return;
        }

        _engine.StartRankingPrompt(state, prompt, RankingPromptDisplaySeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started ranking prompt {PromptIndex}/3 in room {RoomCode}: '{Prompt}'", 
            state.RankingPromptIndex, roomCode, prompt.Prompt);

        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, RankingPromptDisplaySeconds, async () =>
        {
            await TransitionToRankingVotingAsync(roomCode);
        });
    }

    private async Task TransitionToRankingVotingAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartRankingVotingPhase(state, RankingVotingSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, RankingVotingSeconds, async () =>
        {
            await AdvanceFromRankingVotingAsync(roomCode);
        });
    }

    private async Task AdvanceFromRankingVotingAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.RevealRankingVotes(state, RankingRevealSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);
        UpdatePlayerScores(roomCode, state);

        SchedulePhaseTransition(roomCode, RankingRevealSeconds, async () =>
        {
            await HandleRankingRevealCompleteAsync(roomCode);
        });
    }

    /// <summary>
    /// After ranking reveal: go directly to next prompt unless it's the last prompt.
    /// Scoreboard only shows at end of RankingStars round (after prompt 3).
    /// </summary>
    private async Task HandleRankingRevealCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Check if this is the last prompt in the ranking round
        var isEndOfRound = !_engine.HasMoreRankingPrompts(state);

        if (isEndOfRound)
        {
            // Show scoreboard at end of ranking round
            await TransitionToScoreboardAsync(roomCode);
        }
        else
        {
            // Go directly to next prompt (skip scoreboard mid-round)
            await StartNextRankingPromptAsync(roomCode);
        }
    }

    #endregion

    #region Dictionary Game Flow

    private async Task StartDictionaryRoundAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartDictionaryRound(state, DictionaryWordDisplaySeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started Dictionary round {RoundNumber} in room {RoomCode}", 
            state.RoundNumber, roomCode);

        await StartNextDictionaryWordAsync(roomCode);
    }

    private async Task StartNextDictionaryWordAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        var question = _dictionaryProvider.GetRandomQuestion(state.Locale, state.UsedDictionaryWords);
        
        if (question == null)
        {
            _logger.LogWarning("No dictionary words available in room {RoomCode}", roomCode);
            await HandleRoundCompleteAsync(roomCode);
            return;
        }

        _engine.StartDictionaryWord(state, question, DictionaryWordDisplaySeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started dictionary word {WordIndex}/3: '{Word}' in room {RoomCode}", 
            state.DictionaryWordIndex, question.Word, roomCode);

        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, DictionaryWordDisplaySeconds, async () =>
        {
            await TransitionToDictionaryAnsweringAsync(roomCode);
        });
    }

    private async Task TransitionToDictionaryAnsweringAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartDictionaryAnsweringPhase(state, DictionaryAnsweringSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        SchedulePhaseTransition(roomCode, DictionaryAnsweringSeconds, async () =>
        {
            await AdvanceFromDictionaryAnsweringAsync(roomCode);
        });
    }

    private async Task AdvanceFromDictionaryAnsweringAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.RevealDictionaryAnswer(state, DictionaryRevealSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);
        UpdatePlayerScores(roomCode, state);

        SchedulePhaseTransition(roomCode, DictionaryRevealSeconds, async () =>
        {
            await HandleDictionaryRevealCompleteAsync(roomCode);
        });
    }

    /// <summary>
    /// After dictionary reveal: go directly to next word unless it's the last word.
    /// Scoreboard only shows at end of Dictionary round (after word 3).
    /// </summary>
    private async Task HandleDictionaryRevealCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Check if this is the last word in the dictionary round
        var isEndOfRound = !_engine.HasMoreDictionaryWords(state);

        if (isEndOfRound)
        {
            // Show scoreboard at end of dictionary round
            await TransitionToScoreboardAsync(roomCode);
        }
        else
        {
            // Go directly to next word (skip scoreboard mid-round)
            await StartNextDictionaryWordAsync(roomCode);
        }
    }

    #endregion

    #region Utilities

    private async Task<bool> CheckAllAnsweredAndAdvance(
        string roomCode, 
        QuizGameState state, 
        Func<IEnumerable<Guid>, bool> allAnsweredCheck,
        Func<Task> advanceAction)
    {
        if (!_roomStore.TryGetRoom(roomCode, out var room) || room == null)
            return false;

        var connectedPlayerIds = room.Players.Values
            .Where(p => p.IsConnected)
            .Select(p => p.PlayerId);

        if (allAnsweredCheck(connectedPlayerIds))
        {
            _logger.LogInformation("All players answered in room {RoomCode}, advancing early", roomCode);
            CancelTimer(roomCode);
            await advanceAction();
            return true;
        }

        return false;
    }

    private void SchedulePhaseTransition(string roomCode, int delaySeconds, Func<Task> action)
    {
        CancelTimer(roomCode);

        var cts = new CancellationTokenSource();
        _roomTimers[roomCode] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await ExecuteWithRoomLockAsync(roomCode, action);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in phase transition for room {RoomCode}", roomCode);
            }
        }, cts.Token);
    }

    private void CancelTimer(string roomCode)
    {
        if (_roomTimers.TryRemove(roomCode, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task ExecuteWithRoomLockAsync(string roomCode, Func<Task> action)
    {
        var semaphore = _roomLocks.GetOrAdd(roomCode, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<T> ExecuteWithRoomLockAsync<T>(string roomCode, Func<Task<T>> action)
    {
        var semaphore = _roomLocks.GetOrAdd(roomCode, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task BroadcastStateAsync(string roomCode, QuizGameState state)
    {
        var dto = CreateSafeDto(state);
        await _hubContext.Clients.Group($"room:{roomCode}").SendAsync("QuizStateUpdated", dto);
    }

    private QuizGameStateDto CreateSafeDto(QuizGameState state)
    {
        var remainingSeconds = Math.Max(0, (int)(state.PhaseEndsUtc - _clock.UtcNow).TotalSeconds);
        var showCorrectAnswer = state.Phase is QuizPhase.Reveal or QuizPhase.RankingReveal or QuizPhase.Scoreboard or QuizPhase.Finished;

        var answerStatuses = state.Scoreboard
            .Select(p => new PlayerAnswerStatusDto(
                p.PlayerId,
                p.DisplayName,
                GetHasAnswered(state, p.PlayerId)
            ))
            .ToList();

        var questionsInRound = GetQuestionsInRound(state);
        var currentQuestionInRound = GetCurrentQuestionInRound(state);

        // Build player options for ranking (excluding connection IDs, just player info)
        var playerOptions = state.CurrentRound?.Type == RoundType.RankingStars && 
                           state.Phase is QuizPhase.RankingPrompt or QuizPhase.RankingVoting or QuizPhase.RankingReveal
            ? state.Scoreboard.Select(p => new PlayerOptionDto(p.PlayerId, p.DisplayName)).ToList()
            : null;

        // Build booster DTOs
        var playerBoosters = BuildPlayerBoosterDtos(state);
        var activeEffects = BuildActiveEffectDtos(state);

        return new QuizGameStateDto(
            Phase: state.Phase,
            QuestionNumber: state.QuestionNumber,
            TotalQuestions: state.TotalQuestions + QuizGameState.DictionaryWordsPerRound,
            RoundNumber: state.RoundNumber,
            QuestionsInRound: questionsInRound,
            CurrentQuestionInRound: currentQuestionInRound,
            CurrentCategory: state.CurrentRound?.Category,
            RoundLeaderPlayerId: state.CurrentRound?.RoundLeaderPlayerId,
            AvailableCategories: state.Phase == QuizPhase.CategorySelection ? state.AvailableCategories : null,
            RoundType: state.CurrentRound?.Type,
            QuestionId: state.QuestionId,
            QuestionText: GetQuestionText(state),
            Options: GetOptions(state, showCorrectAnswer),
            PlayerOptions: playerOptions,
            CorrectOptionKey: showCorrectAnswer ? GetCorrectOptionKey(state) : null,
            Explanation: showCorrectAnswer ? GetExplanation(state) : null,
            RankingWinnerIds: showCorrectAnswer ? state.RankingResult?.WinnerPlayerIds : null,
            RankingVoteCounts: showCorrectAnswer ? state.RankingResult?.VoteCounts : null,
            RemainingSeconds: remainingSeconds,
            PhaseEndsUtc: state.PhaseEndsUtc,
            AnswerStatuses: answerStatuses,
            Scoreboard: state.Scoreboard
                .Select(p => new PlayerScoreDto(
                    p.PlayerId,
                    p.DisplayName,
                    p.Score,
                    p.Position,
                    showCorrectAnswer ? p.AnsweredCorrectly : null,
                    showCorrectAnswer ? p.SelectedOption : null,
                    showCorrectAnswer ? p.PointsEarned : 0,
                    showCorrectAnswer && p.GotSpeedBonus,
                    showCorrectAnswer && p.IsRankingStar,
                    showCorrectAnswer ? p.RankingVotesReceived : 0
                ))
                .OrderBy(p => p.Position)
                .ToList(),
            PlayerBoosters: playerBoosters,
            ActiveEffects: activeEffects,
            MyAnsweringEffects: null // Set per-player in hub when sending to specific players
        );
    }

    private List<PlayerBoosterStateDto> BuildPlayerBoosterDtos(QuizGameState state)
    {
        var result = new List<PlayerBoosterStateDto>();
        
        foreach (var (playerId, boosterState) in state.PlayerBoosters)
        {
            var handler = _boosterService.GetHandler(boosterState.Type);
            if (handler == null) continue;
            
            result.Add(new PlayerBoosterStateDto(
                PlayerId: playerId,
                BoosterType: boosterState.Type,
                BoosterName: handler.Name,
                BoosterDescription: handler.Description,
                IsUsed: boosterState.IsUsed,
                RequiresTarget: handler.RequiresTarget,
                ValidPhases: handler.ValidPhases.Select(p => p.ToString()).ToArray()
            ));
        }
        
        return result;
    }

    private List<ActiveBoosterEffectDto> BuildActiveEffectDtos(QuizGameState state)
    {
        var effects = _boosterService.GetActiveEffects(state);
        return effects.Select(e =>
        {
            var activator = state.Scoreboard.FirstOrDefault(p => p.PlayerId == e.ActivatorPlayerId);
            var target = e.TargetPlayerId.HasValue 
                ? state.Scoreboard.FirstOrDefault(p => p.PlayerId == e.TargetPlayerId.Value) 
                : null;

            return new ActiveBoosterEffectDto(
                BoosterType: e.BoosterType,
                ActivatorPlayerId: e.ActivatorPlayerId,
                ActivatorName: activator?.DisplayName ?? "Unknown",
                TargetPlayerId: e.TargetPlayerId,
                TargetName: target?.DisplayName,
                QuestionNumber: e.QuestionNumber
            );
        }).ToList();
    }

    private bool GetHasAnswered(QuizGameState state, Guid playerId)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryAnswers.TryGetValue(playerId, out var da) && da != null,
            RoundType.RankingStars => state.RankingVotes.TryGetValue(playerId, out var rv) && rv != null,
            _ => state.Answers.TryGetValue(playerId, out var a) && a != null
        };
    }

    private int GetQuestionsInRound(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => QuizGameState.DictionaryWordsPerRound,
            RoundType.RankingStars => QuizGameState.RankingPromptsPerRound,
            _ => GameRound.QuestionsPerRound
        };
    }

    private int GetCurrentQuestionInRound(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryWordIndex,
            RoundType.RankingStars => state.RankingPromptIndex,
            _ => state.CurrentRound?.CurrentQuestionIndex ?? 0
        };
    }

    private string GetQuestionText(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.Word ?? string.Empty,
            RoundType.RankingStars => state.RankingPromptText ?? string.Empty,
            _ => state.QuestionText
        };
    }

    private List<QuizOptionDto> GetOptions(QuizGameState state, bool showCorrectAnswer)
    {
        if (state.CurrentRound?.Type == RoundType.DictionaryGame)
        {
            if (state.DictionaryQuestion == null || state.Phase == QuizPhase.DictionaryWord)
                return new List<QuizOptionDto>();

            return state.DictionaryQuestion.Options
                .Select((text, index) => new QuizOptionDto(index.ToString(), text))
                .ToList();
        }

        if (state.CurrentRound?.Type == RoundType.RankingStars)
        {
            // For ranking, options are players - handled separately via PlayerOptions
            return new List<QuizOptionDto>();
        }

        return state.Options.Select(o => new QuizOptionDto(o.Key, o.Text)).ToList();
    }

    private string? GetCorrectOptionKey(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.CorrectIndex.ToString(),
            RoundType.RankingStars => state.RankingResult?.WinnerPlayerIds.FirstOrDefault().ToString(),
            _ => state.CorrectOptionKey
        };
    }

    private string? GetExplanation(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.Definition,
            RoundType.RankingStars => null, // No explanation for ranking
            _ => state.Explanation
        };
    }

    private void UpdatePlayerScores(string roomCode, QuizGameState state)
    {
        if (!_roomStore.TryGetRoom(roomCode, out var room) || room == null)
            return;

        foreach (var scoreState in state.Scoreboard)
        {
            if (room.Players.TryGetValue(scoreState.PlayerId, out var player))
            {
                player.Score = scoreState.Score;
            }
        }

        _roomStore.Update(room);
    }

    private void UpdateRoomStatus(string roomCode, RoomStatus status)
    {
        if (!_roomStore.TryGetRoom(roomCode, out var room) || room == null)
            return;

        room.Status = status;
        _roomStore.Update(room);
    }

    #endregion
}
