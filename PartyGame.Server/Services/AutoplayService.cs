using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Core.Models;
using PartyGame.Core.Models.Quiz;
using PartyGame.Server.Options;

namespace PartyGame.Server.Services;

public class AutoplayService : IAutoplayService
{
    private readonly IRoomStore _roomStore;
    private readonly IQuizGameOrchestrator _quizOrchestrator;
    private readonly ILogger<AutoplayService> _logger;
    private readonly AutoplayOptions _options;
    private readonly ConcurrentDictionary<string, RoomAutoplayState> _roomStates = new();

    public AutoplayService(
        IRoomStore roomStore,
        IQuizGameOrchestrator quizOrchestrator,
        IOptions<AutoplayOptions> options,
        ILogger<AutoplayService> logger)
    {
        _roomStore = roomStore;
        _quizOrchestrator = quizOrchestrator;
        _logger = logger;
        _options = options.Value;
    }

    public bool IsRunning(string roomCode)
    {
        var normalized = roomCode.ToUpperInvariant();
        return _roomStates.TryGetValue(normalized, out var state) && state.IsRunning;
    }

    public IReadOnlyCollection<Guid> GetBotIds(string roomCode)
    {
        var normalized = roomCode.ToUpperInvariant();
        if (_roomStates.TryGetValue(normalized, out var state))
        {
            return state.BotIds.ToList();
        }

        if (!_roomStore.TryGetRoom(normalized, out var room) || room == null)
        {
            return Array.Empty<Guid>();
        }

        return room.Players.Values.Where(p => p.IsBot).Select(p => p.PlayerId).ToList();
    }

    public async Task StartAsync(string roomCode)
    {
        var normalized = roomCode.ToUpperInvariant();
        if (_roomStates.ContainsKey(normalized))
        {
            return;
        }

        var state = new RoomAutoplayState(normalized, _options);
        if (!_roomStates.TryAdd(normalized, state))
        {
            return;
        }

        state.BotIds = new HashSet<Guid>(GetBotIds(normalized));

        state.IsRunning = true;
        state.LoopTask = Task.Run(() => RunLoopAsync(state), state.CancellationTokenSource.Token);

        _logger.LogInformation("Autoplay started for room {RoomCode} with {BotCount} bot(s)",
            normalized, state.BotIds.Count);
    }

    public async Task StopAsync(string roomCode)
    {
        var normalized = roomCode.ToUpperInvariant();
        if (!_roomStates.TryRemove(normalized, out var state))
        {
            return;
        }

        state.IsRunning = false;
        state.CancellationTokenSource.Cancel();

        try
        {
            if (state.LoopTask != null)
            {
                await state.LoopTask;
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogDebug(ex,
                "Autoplay loop task was canceled for room {RoomCode} during stop.",
                normalized);
        }
        finally
        {
            // Ensure CancellationTokenSource is disposed even if the loop hasn't completed yet
            state.CancellationTokenSource.Dispose();
        }

        _logger.LogInformation("Autoplay stopped for room {RoomCode} with {BotCount} bot(s)",
            normalized, state.BotIds.Count);
    }

    private async Task RunLoopAsync(RoomAutoplayState state)
    {
        var token = state.CancellationTokenSource.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await TickAsync(state, token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when the cancellation token is triggered; exit the loop.
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Autoplay loop error in room {RoomCode}", state.RoomCode);
                }

                try
                {
                    await Task.Delay(state.Options.PollIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when the cancellation token is triggered; exit the loop.
                    break;
                }
            }
        }
        finally
        {
            state.CancellationTokenSource.Dispose();
        }
    }

    private async Task TickAsync(RoomAutoplayState state, CancellationToken token)
    {
        if (!_roomStore.TryGetRoom(state.RoomCode, out var room) || room == null)
        {
            RequestStop(state, "room_not_found");
            return;
        }

        if (room.Status == RoomStatus.Finished)
        {
            RequestStop(state, "game_finished");
            return;
        }

        var gameState = _quizOrchestrator.GetState(state.RoomCode);
        if (gameState == null || gameState.Phase == QuizPhase.Finished)
        {
            return;
        }

        state.BotIds = new HashSet<Guid>(room.Players.Values.Where(p => p.IsBot).Select(p => p.PlayerId));

        switch (gameState.Phase)
        {
            case QuizPhase.CategorySelection:
                await HandleCategorySelectionAsync(state, gameState, token);
                break;
            case QuizPhase.Answering:
                await HandleQuizAnsweringAsync(state, gameState, token);
                break;
            case QuizPhase.DictionaryAnswering:
                await HandleDictionaryAnsweringAsync(state, gameState, token);
                break;
            case QuizPhase.RankingVoting:
                await HandleRankingVotingAsync(state, gameState, token);
                break;
        }
    }

    private void RequestStop(RoomAutoplayState state, string reason)
    {
        if (_roomStates.TryRemove(state.RoomCode, out _))
        {
            state.IsRunning = false;
            state.CancellationTokenSource.Cancel();
            _logger.LogInformation("Autoplay stopping for room {RoomCode} with {BotCount} bot(s) (reason: {Reason})",
                state.RoomCode, state.BotIds.Count, reason);
        }
    }

    private async Task HandleCategorySelectionAsync(RoomAutoplayState state, QuizGameState gameState, CancellationToken token)
    {
        var leaderId = gameState.CurrentRound?.RoundLeaderPlayerId;
        if (!leaderId.HasValue || !state.BotIds.Contains(leaderId.Value))
        {
            return;
        }

        if (gameState.AvailableCategories.Count == 0)
        {
            return;
        }

        var phaseKey = BuildPhaseKey(gameState);
        if (!state.ActionTracker.ShouldActNow(leaderId.Value, phaseKey))
        {
            return;
        }

        var category = gameState.AvailableCategories[state.Random.Next(gameState.AvailableCategories.Count)];
        var delay = GetRandomDelay(state);

        _logger.LogInformation("Autoplay scheduled category selection in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
            state.RoomCode, state.BotIds.Count, gameState.Phase);

        await ScheduleActionAsync(state, delay, token, async () =>
        {
            var latestState = _quizOrchestrator.GetState(state.RoomCode);
            if (latestState == null || latestState.Phase != QuizPhase.CategorySelection)
            {
                return;
            }

            await _quizOrchestrator.SelectCategoryAsync(state.RoomCode, leaderId.Value, category);

            _logger.LogInformation("Autoplay action: SelectCategory in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                state.RoomCode, state.BotIds.Count, latestState.Phase);
        });
    }

    private async Task HandleQuizAnsweringAsync(RoomAutoplayState state, QuizGameState gameState, CancellationToken token)
    {
        var phaseKey = BuildPhaseKey(gameState);
        foreach (var botId in state.BotIds)
        {
            if (!gameState.Answers.TryGetValue(botId, out var current) || current != null)
            {
                continue;
            }

            if (!state.ActionTracker.ShouldActNow(botId, phaseKey))
            {
                continue;
            }

            var delay = GetRandomDelay(state);
            var optionKey = ChooseQuizOption(gameState, botId, state);

            _logger.LogInformation("Autoplay scheduled answer in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                state.RoomCode, state.BotIds.Count, gameState.Phase);

            await ScheduleActionAsync(state, delay, token, async () =>
            {
                var latestState = _quizOrchestrator.GetState(state.RoomCode);
                if (latestState == null || latestState.Phase != QuizPhase.Answering)
                {
                    return;
                }

                if (latestState.Answers.TryGetValue(botId, out var latestAnswer) && latestAnswer == null)
                {
                    await _quizOrchestrator.SubmitAnswerAsync(state.RoomCode, botId, optionKey);
                    _logger.LogInformation("Autoplay action: SubmitAnswer in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                        state.RoomCode, state.BotIds.Count, latestState.Phase);
                }
            });
        }
    }

    private async Task HandleDictionaryAnsweringAsync(RoomAutoplayState state, QuizGameState gameState, CancellationToken token)
    {
        var phaseKey = BuildPhaseKey(gameState);
        foreach (var botId in state.BotIds)
        {
            if (!gameState.DictionaryAnswers.TryGetValue(botId, out var current) || current != null)
            {
                continue;
            }

            if (!state.ActionTracker.ShouldActNow(botId, phaseKey))
            {
                continue;
            }

            var delay = GetRandomDelay(state);
            var optionIndex = ChooseDictionaryOption(gameState, botId, state);

            _logger.LogInformation("Autoplay scheduled dictionary answer in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                state.RoomCode, state.BotIds.Count, gameState.Phase);

            await ScheduleActionAsync(state, delay, token, async () =>
            {
                var latestState = _quizOrchestrator.GetState(state.RoomCode);
                if (latestState == null || latestState.Phase != QuizPhase.DictionaryAnswering)
                {
                    return;
                }

                if (latestState.DictionaryAnswers.TryGetValue(botId, out var latestAnswer) && latestAnswer == null)
                {
                    await _quizOrchestrator.SubmitDictionaryAnswerAsync(state.RoomCode, botId, optionIndex);
                    _logger.LogInformation("Autoplay action: SubmitDictionaryAnswer in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                        state.RoomCode, state.BotIds.Count, latestState.Phase);
                }
            });
        }
    }

    private async Task HandleRankingVotingAsync(RoomAutoplayState state, QuizGameState gameState, CancellationToken token)
    {
        var phaseKey = BuildPhaseKey(gameState);
        var playerIds = gameState.Scoreboard.Select(p => p.PlayerId).ToList();
        foreach (var botId in state.BotIds)
        {
            if (!gameState.RankingVotes.TryGetValue(botId, out var current) || current != null)
            {
                continue;
            }

            if (!state.ActionTracker.ShouldActNow(botId, phaseKey))
            {
                continue;
            }

            var delay = GetRandomDelay(state);
            var voteTarget = ChooseRankingVote(botId, playerIds, state);
            if (voteTarget == Guid.Empty)
            {
                continue;
            }

            _logger.LogInformation("Autoplay scheduled ranking vote in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                state.RoomCode, state.BotIds.Count, gameState.Phase);

            await ScheduleActionAsync(state, delay, token, async () =>
            {
                var latestState = _quizOrchestrator.GetState(state.RoomCode);
                if (latestState == null || latestState.Phase != QuizPhase.RankingVoting)
                {
                    return;
                }

                if (latestState.RankingVotes.TryGetValue(botId, out var latestVote) && latestVote == null)
                {
                    await _quizOrchestrator.SubmitRankingVoteAsync(state.RoomCode, botId, voteTarget);
                    _logger.LogInformation("Autoplay action: SubmitRankingVote in room {RoomCode} with {BotCount} bot(s) (phase {Phase})",
                        state.RoomCode, state.BotIds.Count, latestState.Phase);
                }
            });
        }
    }

    private async Task ScheduleActionAsync(RoomAutoplayState state, int delayMs, CancellationToken token, Func<Task> action)
    {
        try
        {
            await Task.Delay(delayMs, token);
            if (!token.IsCancellationRequested)
            {
                try
                {
                    await action();
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex,
                        "Unhandled exception in scheduled autoplay action for room {RoomCode}",
                        state.RoomCode);
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private static string BuildPhaseKey(QuizGameState state)
    {
        var questionKey = string.IsNullOrEmpty(state.QuestionId) ? "none" : state.QuestionId;
        var rankingKey = state.RankingPromptId ?? "none";
        return $"{state.Phase}:{state.RoundNumber}:{state.QuestionNumber}:{questionKey}:{state.DictionaryWordIndex}:{rankingKey}";
    }

    private static int GetRandomDelay(RoomAutoplayState state)
    {
        var min = Math.Max(0, state.Options.MinActionDelayMs);
        var max = Math.Max(min, state.Options.MaxActionDelayMs);
        return state.Random.Next(min, max + 1);
    }

    private string ChooseQuizOption(QuizGameState state, Guid botId, RoomAutoplayState roomState)
    {
        var skill = GetBotSkill(botId, roomState);
        var correctKey = state.CorrectOptionKey ?? state.Options.FirstOrDefault()?.Key ?? "A";

        var roll = roomState.Random.Next(0, 100);
        if (roll < skill)
        {
            return correctKey;
        }

        var options = state.Options.Select(o => o.Key).Where(k => !string.Equals(k, correctKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (options.Count == 0)
        {
            return correctKey;
        }

        return options[roomState.Random.Next(options.Count)];
    }

    private int ChooseDictionaryOption(QuizGameState state, Guid botId, RoomAutoplayState roomState)
    {
        var skill = GetBotSkill(botId, roomState);
        var correctIndex = state.DictionaryQuestion?.CorrectIndex ?? 0;

        var roll = roomState.Random.Next(0, 100);
        if (roll < skill)
        {
            return correctIndex;
        }

        var options = Enumerable.Range(0, 4).Where(i => i != correctIndex).ToList();
        if (options.Count == 0)
        {
            return correctIndex;
        }

        return options[roomState.Random.Next(options.Count)];
    }

    private static Guid ChooseRankingVote(Guid botId, List<Guid> playerIds, RoomAutoplayState roomState)
    {
        var eligible = playerIds.Where(id => id != botId).ToList();
        if (eligible.Count == 0)
        {
            return Guid.Empty;
        }

        return eligible[roomState.Random.Next(eligible.Count)];
    }

    private int GetBotSkill(Guid botId, RoomAutoplayState roomState)
    {
        if (!_roomStore.TryGetRoom(roomState.RoomCode, out var room) || room == null)
        {
            return 50;
        }

        if (room.Players.TryGetValue(botId, out var player) &&
            player.BotSkill >= 0 && player.BotSkill <= 100)
        {
            return player.BotSkill;
        }

        return 50;
    }

    private sealed class RoomAutoplayState
    {
        public RoomAutoplayState(string roomCode, AutoplayOptions options)
        {
            RoomCode = roomCode;
            Options = options;
        }

        public string RoomCode { get; }
        public AutoplayOptions Options { get; }
        public CancellationTokenSource CancellationTokenSource { get; } = new();
        public Random Random => Random.Shared;
        public HashSet<Guid> BotIds { get; set; } = new();
        public BotActionTracker ActionTracker { get; } = new();
        public bool IsRunning { get; set; }
        public Task? LoopTask { get; set; }
    }
}
