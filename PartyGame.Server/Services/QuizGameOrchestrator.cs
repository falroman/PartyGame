using Microsoft.AspNetCore.SignalR;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.DTOs;
using PartyGame.Server.Hubs;
using System.Collections.Concurrent;

namespace PartyGame.Server.Services;

/// <summary>
/// Orchestrates quiz game flow including timers and broadcasting.
/// </summary>
public interface IQuizGameOrchestrator
{
    /// <summary>
    /// Starts a new quiz game for a room.
    /// </summary>
    Task<(bool Success, ErrorDto? Error)> StartGameAsync(Room room);

    /// <summary>
    /// Selects a category for the current round.
    /// </summary>
    Task<(bool Success, ErrorDto? Error)> SelectCategoryAsync(string roomCode, Guid playerId, string category);

    /// <summary>
    /// Submits a player's answer.
    /// </summary>
    Task<(bool Success, ErrorDto? Error)> SubmitAnswerAsync(string roomCode, Guid playerId, string optionKey);

    /// <summary>
    /// Advances to the next question (host-triggered).
    /// </summary>
    Task<(bool Success, ErrorDto? Error)> NextQuestionAsync(string roomCode, string connectionId);

    /// <summary>
    /// Gets the current quiz state for a room.
    /// </summary>
    QuizGameState? GetState(string roomCode);

    /// <summary>
    /// Stops the game timer for a room (cleanup).
    /// </summary>
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

    // Active game states per room
    private readonly ConcurrentDictionary<string, QuizGameState> _gameStates = new();
    
    // Active timers per room (prevents duplicate timers)
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _roomTimers = new();

    // Game configuration
    private const int CategorySelectionSeconds = 30; // Time to select category
    private const int QuestionDisplaySeconds = 3; // Time to show question before answering
    private const int AnsweringSeconds = 15; // Time to answer
    private const int RevealSeconds = 5; // Time to show correct answer
    private const int ScoreboardSeconds = 5; // Time to show scoreboard

    public QuizGameOrchestrator(
        IQuizGameEngine engine,
        IRoomStore roomStore,
        IClock clock,
        IHubContext<GameHub> hubContext,
        ILogger<QuizGameOrchestrator> logger)
    {
        _engine = engine;
        _roomStore = roomStore;
        _clock = clock;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> StartGameAsync(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        var roomCode = room.Code.ToUpperInvariant();

        // Initialize game state
        var state = _engine.InitializeGame(room, "nl-BE", 9); // 3 rounds x 3 questions = 9
        _gameStates[roomCode] = state;

        _logger.LogInformation("Quiz game initialized for room {RoomCode}", roomCode);

        // Start first round (category selection)
        await StartNewRoundAsync(roomCode);

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> SelectCategoryAsync(string roomCode, Guid playerId, string category)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_gameStates.TryGetValue(normalizedCode, out var state))
        {
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));
        }

        // Check phase
        if (state.Phase != QuizPhase.CategorySelection)
        {
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Category selection is not active."));
        }

        // Check if player is the round leader
        if (state.CurrentRound?.RoundLeaderPlayerId != playerId)
        {
            return (false, new ErrorDto(ErrorCodes.NotRoundLeader, "Only the round leader can select a category."));
        }

        // Check if category has already been selected
        if (!string.IsNullOrEmpty(state.CurrentRound.Category))
        {
            return (false, new ErrorDto(ErrorCodes.RoundAlreadyStarted, "Category has already been selected."));
        }

        // Validate category
        if (!_engine.IsValidCategory(state, category))
        {
            return (false, new ErrorDto(ErrorCodes.InvalidCategory, "Invalid category selection."));
        }

        // Set the category
        _engine.SetRoundCategory(state, category);
        
        _logger.LogInformation("Round leader {PlayerId} selected category '{Category}' in room {RoomCode}", 
            playerId, category, normalizedCode);

        // Cancel the category selection timer
        CancelTimer(normalizedCode);

        // Broadcast state with selected category
        await BroadcastStateAsync(normalizedCode, state);

        // Short delay to show the selected category, then start first question
        SchedulePhaseTransition(normalizedCode, 2, async () =>
        {
            await StartNextQuestionAsync(normalizedCode);
        });

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> SubmitAnswerAsync(string roomCode, Guid playerId, string optionKey)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_gameStates.TryGetValue(normalizedCode, out var state))
        {
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));
        }

        // Check phase
        if (state.Phase != QuizPhase.Answering)
        {
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Answers are not being accepted at this time."));
        }

        // Check if player is in the game
        if (!state.Answers.ContainsKey(playerId))
        {
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Player is not in this game."));
        }

        // Validate option
        if (!_engine.IsValidOptionKey(state, optionKey))
        {
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Invalid option."));
        }

        // Submit answer
        _engine.SubmitAnswer(state, playerId, optionKey);

        _logger.LogInformation("Player {PlayerId} submitted answer {Option} in room {RoomCode}", 
            playerId, optionKey, normalizedCode);

        // Broadcast updated state
        await BroadcastStateAsync(normalizedCode, state);

        // Check if all players answered - advance early if so
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return (true, null);
        }

        var connectedPlayerIds = room.Players.Values
            .Where(p => p.IsConnected)
            .Select(p => p.PlayerId);

        if (_engine.AllPlayersAnswered(state, connectedPlayerIds))
        {
            _logger.LogInformation("All players answered in room {RoomCode}, advancing early", normalizedCode);
            CancelTimer(normalizedCode);
            await AdvanceFromAnsweringAsync(normalizedCode);
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, ErrorDto? Error)> NextQuestionAsync(string roomCode, string connectionId)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        if (!_gameStates.TryGetValue(normalizedCode, out var state))
        {
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Game not found."));
        }

        // Verify host
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return (false, new ErrorDto(ErrorCodes.RoomNotFound, "Room not found."));
        }

        if (room.HostConnectionId != connectionId)
        {
            return (false, new ErrorDto(ErrorCodes.NotHost, "Only the host can advance the game."));
        }

        // Only allow from Scoreboard phase
        if (state.Phase != QuizPhase.Scoreboard)
        {
            return (false, new ErrorDto(ErrorCodes.InvalidState, "Cannot advance at this time."));
        }

        CancelTimer(normalizedCode);
        await HandleScoreboardCompleteAsync(normalizedCode);

        return (true, null);
    }

    /// <inheritdoc />
    public QuizGameState? GetState(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        return _gameStates.TryGetValue(normalizedCode, out var state) ? state : null;
    }

    /// <inheritdoc />
    public void StopGame(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();
        CancelTimer(normalizedCode);
        _gameStates.TryRemove(normalizedCode, out _);
        _logger.LogInformation("Quiz game stopped for room {RoomCode}", normalizedCode);
    }

    private async Task StartNewRoundAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.StartNewRound(state, CategorySelectionSeconds, _clock.UtcNow);
        
        _logger.LogInformation("Started round {RoundNumber} in room {RoomCode}, leader: {LeaderId}", 
            state.RoundNumber, roomCode, state.CurrentRound?.RoundLeaderPlayerId);

        await BroadcastStateAsync(roomCode, state);

        // Schedule auto-select if no category chosen in time (picks first available)
        SchedulePhaseTransition(roomCode, CategorySelectionSeconds, async () =>
        {
            await AutoSelectCategoryAsync(roomCode);
        });
    }

    private async Task AutoSelectCategoryAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Only auto-select if still in CategorySelection phase
        if (state.Phase != QuizPhase.CategorySelection)
            return;

        // Pick first available category
        var category = state.AvailableCategories.FirstOrDefault();
        if (string.IsNullOrEmpty(category))
        {
            _logger.LogWarning("No categories available for auto-select in room {RoomCode}", roomCode);
            return;
        }

        _engine.SetRoundCategory(state, category);
        _logger.LogInformation("Auto-selected category '{Category}' in room {RoomCode}", category, roomCode);

        await BroadcastStateAsync(roomCode, state);

        // Start first question
        SchedulePhaseTransition(roomCode, 2, async () =>
        {
            await StartNextQuestionAsync(roomCode);
        });
    }

    private async Task StartNextQuestionAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        var newState = _engine.StartNewQuestion(state, QuestionDisplaySeconds, _clock.UtcNow);
        
        if (newState == null)
        {
            // No more questions, finish game
            _engine.FinishGame(state);
            await BroadcastStateAsync(roomCode, state);
            UpdateRoomStatus(roomCode, RoomStatus.Finished);
            return;
        }

        _gameStates[roomCode] = newState;
        await BroadcastStateAsync(roomCode, newState);

        // Schedule transition to Answering phase
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

        // Schedule transition to Reveal phase
        SchedulePhaseTransition(roomCode, AnsweringSeconds, async () =>
        {
            await AdvanceFromAnsweringAsync(roomCode);
        });
    }

    private async Task AdvanceFromAnsweringAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Transition to Reveal
        _engine.RevealAnswer(state, RevealSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        // Update player scores in room
        UpdatePlayerScores(roomCode, state);

        // Schedule transition to Scoreboard
        SchedulePhaseTransition(roomCode, RevealSeconds, async () =>
        {
            await TransitionToScoreboardAsync(roomCode);
        });
    }

    private async Task TransitionToScoreboardAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        _engine.ShowScoreboard(state, ScoreboardSeconds, _clock.UtcNow);
        await BroadcastStateAsync(roomCode, state);

        // Schedule next action after scoreboard
        SchedulePhaseTransition(roomCode, ScoreboardSeconds, async () =>
        {
            await HandleScoreboardCompleteAsync(roomCode);
        });
    }

    private async Task HandleScoreboardCompleteAsync(string roomCode)
    {
        if (!_gameStates.TryGetValue(roomCode, out var state))
            return;

        // Check if game should end
        if (!_engine.HasMoreQuestions(state))
        {
            // Game finished
            _engine.FinishGame(state);
            await BroadcastStateAsync(roomCode, state);
            UpdateRoomStatus(roomCode, RoomStatus.Finished);
            return;
        }

        // Check if round is complete
        if (!_engine.HasMoreQuestionsInRound(state))
        {
            // Round complete, start new round
            _logger.LogInformation("Round {RoundNumber} complete in room {RoomCode}, starting new round", 
                state.RoundNumber, roomCode);
            await StartNewRoundAsync(roomCode);
        }
        else
        {
            // More questions in this round
            await StartNextQuestionAsync(roomCode);
        }
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
                    await action();
                }
            }
            catch (TaskCanceledException)
            {
                // Timer was cancelled, ignore
            }
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

    private async Task BroadcastStateAsync(string roomCode, QuizGameState state)
    {
        var dto = CreateSafeDto(state);
        await _hubContext.Clients.Group($"room:{roomCode}").SendAsync("QuizStateUpdated", dto);
    }

    private QuizGameStateDto CreateSafeDto(QuizGameState state)
    {
        var remainingSeconds = Math.Max(0, (int)(state.PhaseEndsUtc - _clock.UtcNow).TotalSeconds);

        // Build answer statuses (who has answered, not what)
        var answerStatuses = state.Scoreboard
            .Select(p => new PlayerAnswerStatusDto(
                p.PlayerId,
                p.DisplayName,
                state.Answers.TryGetValue(p.PlayerId, out var a) && a != null
            ))
            .ToList();

        // In Answering phase, hide correct answer
        // In Reveal/Scoreboard/Finished, show correct answer
        var showCorrectAnswer = state.Phase is QuizPhase.Reveal or QuizPhase.Scoreboard or QuizPhase.Finished;

        // Calculate questions in current round
        var questionsInRound = GameRound.QuestionsPerRound;
        var currentQuestionInRound = state.CurrentRound?.CurrentQuestionIndex ?? 0;

        return new QuizGameStateDto(
            Phase: state.Phase,
            QuestionNumber: state.QuestionNumber,
            TotalQuestions: state.TotalQuestions,
            RoundNumber: state.RoundNumber,
            QuestionsInRound: questionsInRound,
            CurrentQuestionInRound: currentQuestionInRound,
            CurrentCategory: state.CurrentRound?.Category,
            RoundLeaderPlayerId: state.CurrentRound?.RoundLeaderPlayerId,
            AvailableCategories: state.Phase == QuizPhase.CategorySelection ? state.AvailableCategories : null,
            QuestionId: state.QuestionId,
            QuestionText: state.QuestionText,
            Options: state.Options.Select(o => new QuizOptionDto(o.Key, o.Text)).ToList(),
            CorrectOptionKey: showCorrectAnswer ? state.CorrectOptionKey : null,
            Explanation: showCorrectAnswer ? state.Explanation : null,
            RemainingSeconds: remainingSeconds,
            AnswerStatuses: answerStatuses,
            Scoreboard: state.Scoreboard
                .Select(p => new PlayerScoreDto(
                    p.PlayerId,
                    p.DisplayName,
                    p.Score,
                    p.Position,
                    showCorrectAnswer ? p.AnsweredCorrectly : null,
                    showCorrectAnswer ? p.SelectedOption : null
                ))
                .OrderBy(p => p.Position)
                .ToList()
        );
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
}
