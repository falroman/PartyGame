using Microsoft.AspNetCore.Mvc;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Server.Services;
using PartyGame.Server.Services.Boosters;

namespace PartyGame.Server.Controllers;

/// <summary>
/// DEV ONLY: Controller for testing booster assignments.
/// Remove in production or protect with authentication.
/// </summary>
[ApiController]
[Route("api/dev/boosters")]
public class BoosterTestController : ControllerBase
{
    private readonly IQuizGameOrchestrator _orchestrator;
    private readonly IBoosterService _boosterService;
    private readonly ILogger<BoosterTestController> _logger;

    public BoosterTestController(
        IQuizGameOrchestrator orchestrator,
        IBoosterService boosterService,
        ILogger<BoosterTestController> logger)
    {
        _orchestrator = orchestrator;
        _boosterService = boosterService;
        _logger = logger;
    }

    /// <summary>
    /// DEV ONLY: Assign a specific booster to a player.
    /// POST /api/dev/boosters/assign?roomCode=ABCD&playerId=guid&boosterType=8
    /// </summary>
    [HttpPost("assign")]
    public IActionResult AssignBooster(
        [FromQuery] string roomCode,
        [FromQuery] Guid playerId,
        [FromQuery] int boosterType)
    {
#if !DEBUG
        return NotFound(); // Only available in DEBUG builds
#endif

        var state = _orchestrator.GetState(roomCode);
        if (state == null)
        {
            return NotFound(new { error = "Room not found or game not started" });
        }

        if (!state.PlayerBoosters.ContainsKey(playerId))
        {
            return NotFound(new { error = "Player not in game" });
        }

        var boosterEnum = (BoosterType)boosterType;
        _boosterService.AssignBooster(state, playerId, boosterEnum);

        _logger.LogInformation(
            "DEV: Assigned {BoosterType} to player {PlayerId} in room {RoomCode}",
            boosterEnum, playerId, roomCode);

        return Ok(new
        {
            success = true,
            playerId,
            boosterType = boosterEnum.ToString(),
            message = $"Assigned {boosterEnum} to player"
        });
    }
}
