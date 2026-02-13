using Microsoft.AspNetCore.Mvc;
using PartyGame.Core.Interfaces;
using PartyGame.Server.DTOs;
using PartyGame.Server.Services;

namespace PartyGame.Server.Controllers;

/// <summary>
/// REST API controller for quiz game state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    private readonly IRoomStore _roomStore;
    private readonly IQuizGameOrchestrator _quizOrchestrator;
    private readonly ILogger<QuizController> _logger;

    public QuizController(
        IRoomStore roomStore,
        IQuizGameOrchestrator quizOrchestrator,
        ILogger<QuizController> logger)
    {
        _roomStore = roomStore;
        _quizOrchestrator = quizOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current quiz state for a room.
    /// </summary>
    /// <param name="roomCode">The room code.</param>
    /// <returns>The quiz state or 404 if not found.</returns>
    [HttpGet("{roomCode}/state")]
    [ProducesResponseType(typeof(QuizGameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status404NotFound)]
    public ActionResult<QuizGameStateDto> GetQuizState(string roomCode)
    {
        var normalizedCode = roomCode.ToUpperInvariant();

        // Check if room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            _logger.LogWarning("GetQuizState failed: Room {RoomCode} not found", normalizedCode);
            return NotFound(new ErrorDto("ROOM_NOT_FOUND", $"Room with code '{normalizedCode}' does not exist."));
        }

        // Get quiz state as DTO (no player-specific data for API calls)
        var quizStateDto = _quizOrchestrator.GetStateDto(normalizedCode, requestingPlayerId: null);
        
        if (quizStateDto == null)
        {
            _logger.LogWarning("GetQuizState failed: No quiz state for room {RoomCode}", normalizedCode);
            return NotFound(new ErrorDto("QUIZ_NOT_FOUND", $"No active quiz game in room '{normalizedCode}'."));
        }

        _logger.LogDebug("Quiz state retrieved for room {RoomCode}: Phase={Phase}, QuestionNumber={QuestionNumber}", 
            normalizedCode, quizStateDto.Phase, quizStateDto.QuestionNumber);

        return Ok(quizStateDto);
    }
}
