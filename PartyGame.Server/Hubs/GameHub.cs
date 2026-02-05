using Microsoft.AspNetCore.SignalR;
using PartyGame.Core.Enums;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.DTOs;
using PartyGame.Server.Services;

namespace PartyGame.Server.Hubs;

/// <summary>
/// Main SignalR hub for realtime game communication.
/// </summary>
public class GameHub : Hub
{
    private readonly ILobbyService _lobbyService;
    private readonly IQuizGameOrchestrator _quizOrchestrator;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        ILobbyService lobbyService, 
        IQuizGameOrchestrator quizOrchestrator,
        ILogger<GameHub> logger)
    {
        _lobbyService = lobbyService;
        _quizOrchestrator = quizOrchestrator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        
        await _lobbyService.HandleDisconnectAsync(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Host registers themselves for a room after creating it via REST API.
    /// </summary>
    public async Task RegisterHost(string roomCode)
    {
        _logger.LogInformation("RegisterHost called for room {RoomCode} by {ConnectionId}", 
            roomCode, Context.ConnectionId);

        var (success, error) = await _lobbyService.RegisterHostAsync(roomCode, Context.ConnectionId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Add host to room group for receiving broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");

        // Send current room state to host
        var roomState = _lobbyService.GetRoomState(roomCode);
        if (roomState != null)
        {
            await Clients.Caller.SendAsync("LobbyUpdated", roomState);
        }

        // Send current quiz state if game is in progress
        var quizState = _quizOrchestrator.GetState(roomCode);
        if (quizState != null)
        {
            // The orchestrator will have already broadcasted, but send to newly connected host
            await Clients.Caller.SendAsync("QuizStateUpdated", CreateSafeQuizDto(quizState));
        }
    }

    /// <summary>
    /// Player joins a room with their playerId and display name.
    /// </summary>
    public async Task JoinRoom(string roomCode, Guid playerId, string displayName)
    {
        _logger.LogInformation("JoinRoom called for room {RoomCode} by player {PlayerId} ({DisplayName})", 
            roomCode, playerId, displayName);

        var (success, error) = await _lobbyService.JoinRoomAsync(roomCode, playerId, displayName, Context.ConnectionId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Add player to room group for receiving broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");

        // Send current room state to player
        var roomState = _lobbyService.GetRoomState(roomCode);
        if (roomState != null)
        {
            await Clients.Caller.SendAsync("LobbyUpdated", roomState);
        }

        // Send current quiz state if game is in progress
        var quizState = _quizOrchestrator.GetState(roomCode);
        if (quizState != null)
        {
            await Clients.Caller.SendAsync("QuizStateUpdated", CreateSafeQuizDto(quizState));
        }
    }

    /// <summary>
    /// Player leaves a room voluntarily.
    /// </summary>
    public async Task LeaveRoom(string roomCode, Guid playerId)
    {
        _logger.LogInformation("LeaveRoom called for room {RoomCode} by player {PlayerId}", 
            roomCode, playerId);

        await _lobbyService.LeaveRoomAsync(roomCode, playerId);

        // Remove from room group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomCode.ToUpperInvariant()}");
    }

    /// <summary>
    /// Host sets the locked state of a room.
    /// Only the host can lock or unlock a room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="isLocked">True to lock the room, false to unlock.</param>
    public async Task SetRoomLocked(string roomCode, bool isLocked)
    {
        _logger.LogInformation("SetRoomLocked called for room {RoomCode} with isLocked={IsLocked} by {ConnectionId}", 
            roomCode, isLocked, Context.ConnectionId);

        var (success, error) = await _lobbyService.SetRoomLockedAsync(roomCode, Context.ConnectionId, isLocked);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Host starts the game. Only the host can start a game, and at least 2 players are required.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="gameType">The type of game to start (e.g., "Quiz").</param>
    public async Task StartGame(string roomCode, string gameType)
    {
        _logger.LogInformation("StartGame called for room {RoomCode} with gameType={GameType} by {ConnectionId}", 
            roomCode, gameType, Context.ConnectionId);

        // Parse game type from string
        if (!Enum.TryParse<GameType>(gameType, ignoreCase: true, out var parsedGameType))
        {
            _logger.LogWarning("StartGame failed: Invalid game type '{GameType}'", gameType);
            await Clients.Caller.SendAsync("Error", new ErrorDto(ErrorCodes.InvalidState, $"Invalid game type: {gameType}"));
            return;
        }

        var (success, error) = await _lobbyService.StartGameAsync(roomCode, Context.ConnectionId, parsedGameType);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Host selects a category for the current round. Only the host can select a category.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's ID (must be the round host).</param>
    /// <param name="category">The selected category name.</param>
    public async Task SelectCategory(string roomCode, Guid playerId, string category)
    {
        _logger.LogInformation("SelectCategory called for room {RoomCode} by player {PlayerId} with category '{Category}'", 
            roomCode, playerId, category);

        var (success, error) = await _quizOrchestrator.SelectCategoryAsync(roomCode, playerId, category);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Player submits their answer for the current question.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's ID.</param>
    /// <param name="optionKey">The selected option key (A, B, C, D).</param>
    public async Task SubmitAnswer(string roomCode, Guid playerId, string optionKey)
    {
        _logger.LogInformation("SubmitAnswer called for room {RoomCode} by player {PlayerId} with option {OptionKey}", 
            roomCode, playerId, optionKey);

        var (success, error) = await _quizOrchestrator.SubmitAnswerAsync(roomCode, playerId, optionKey);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Player submits their answer for the dictionary game.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="playerId">The player's ID.</param>
    /// <param name="optionIndex">The selected option index (0-3).</param>
    public async Task SubmitDictionaryAnswer(string roomCode, Guid playerId, int optionIndex)
    {
        _logger.LogInformation("SubmitDictionaryAnswer called for room {RoomCode} by player {PlayerId} with option {OptionIndex}", 
            roomCode, playerId, optionIndex);

        var (success, error) = await _quizOrchestrator.SubmitDictionaryAnswerAsync(roomCode, playerId, optionIndex);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Player submits their vote for the Ranking Stars round.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <param name="voterPlayerId">The voting player's ID.</param>
    /// <param name="votedForPlayerId">The player being voted for.</param>
    public async Task SubmitRankingVote(string roomCode, Guid voterPlayerId, Guid votedForPlayerId)
    {
        _logger.LogInformation("SubmitRankingVote called for room {RoomCode} by player {VoterId} voting for {VotedForId}", 
            roomCode, voterPlayerId, votedForPlayerId);

        var (success, error) = await _quizOrchestrator.SubmitRankingVoteAsync(roomCode, voterPlayerId, votedForPlayerId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    /// <summary>
    /// Host triggers the next question (optional - game auto-advances by default).
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    public async Task NextQuestion(string roomCode)
    {
        _logger.LogInformation("NextQuestion called for room {RoomCode} by {ConnectionId}", 
            roomCode, Context.ConnectionId);

        var (success, error) = await _quizOrchestrator.NextQuestionAsync(roomCode, Context.ConnectionId);

        if (!success && error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
        }
    }

    private QuizGameStateDto CreateSafeQuizDto(QuizGameState state)
    {
        var showCorrectAnswer = state.Phase is QuizPhase.Reveal or QuizPhase.RankingReveal or QuizPhase.Scoreboard or QuizPhase.Finished;
        var remainingSeconds = Math.Max(0, (int)(state.PhaseEndsUtc - DateTime.UtcNow).TotalSeconds);

        var answerStatuses = state.Scoreboard
            .Select(p => new PlayerAnswerStatusDto(
                p.PlayerId,
                p.DisplayName,
                GetHasAnswered(state, p.PlayerId)
            ))
            .ToList();

        var questionsInRound = GetQuestionsInRound(state);
        var currentQuestionInRound = GetCurrentQuestionInRound(state);

        var playerOptions = state.CurrentRound?.Type == RoundType.RankingStars && 
                           state.Phase is QuizPhase.RankingPrompt or QuizPhase.RankingVoting or QuizPhase.RankingReveal
            ? state.Scoreboard.Select(p => new PlayerOptionDto(p.PlayerId, p.DisplayName)).ToList()
            : null;

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
            Options: GetOptions(state),
            PlayerOptions: playerOptions,
            CorrectOptionKey: showCorrectAnswer ? GetCorrectOptionKey(state) : null,
            Explanation: showCorrectAnswer ? GetExplanation(state) : null,
            RankingWinnerIds: showCorrectAnswer ? state.RankingResult?.WinnerPlayerIds : null,
            RankingVoteCounts: showCorrectAnswer ? state.RankingResult?.VoteCounts : null,
            RemainingSeconds: remainingSeconds,
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
                .ToList()
        );
    }

    private static bool GetHasAnswered(QuizGameState state, Guid playerId)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryAnswers.TryGetValue(playerId, out var da) && da != null,
            RoundType.RankingStars => state.RankingVotes.TryGetValue(playerId, out var rv) && rv != null,
            _ => state.Answers.TryGetValue(playerId, out var a) && a != null
        };
    }

    private static int GetQuestionsInRound(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => QuizGameState.DictionaryWordsPerRound,
            RoundType.RankingStars => QuizGameState.RankingPromptsPerRound,
            _ => GameRound.QuestionsPerRound
        };
    }

    private static int GetCurrentQuestionInRound(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryWordIndex,
            RoundType.RankingStars => state.RankingPromptIndex,
            _ => state.CurrentRound?.CurrentQuestionIndex ?? 0
        };
    }

    private static string GetQuestionText(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.Word ?? string.Empty,
            RoundType.RankingStars => state.RankingPromptText ?? string.Empty,
            _ => state.QuestionText
        };
    }

    private static List<QuizOptionDto> GetOptions(QuizGameState state)
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
            return new List<QuizOptionDto>();
        }

        return state.Options.Select(o => new QuizOptionDto(o.Key, o.Text)).ToList();
    }

    private static string? GetCorrectOptionKey(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.CorrectIndex.ToString(),
            RoundType.RankingStars => state.RankingResult?.WinnerPlayerIds.FirstOrDefault().ToString(),
            _ => state.CorrectOptionKey
        };
    }

    private static string? GetExplanation(QuizGameState state)
    {
        return state.CurrentRound?.Type switch
        {
            RoundType.DictionaryGame => state.DictionaryQuestion?.Definition,
            RoundType.RankingStars => null,
            _ => state.Explanation
        };
    }
}
