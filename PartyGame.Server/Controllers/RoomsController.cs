using Microsoft.AspNetCore.Mvc;
using PartyGame.Core.Interfaces;
using PartyGame.Server.DTOs;

namespace PartyGame.Server.Controllers;

/// <summary>
/// REST API controller for room management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomStore _roomStore;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomStore roomStore, ILogger<RoomsController> logger)
    {
        _roomStore = roomStore;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new game room.
    /// </summary>
    /// <returns>The created room information.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateRoomResponseDto), StatusCodes.Status201Created)]
    public ActionResult<CreateRoomResponseDto> CreateRoom()
    {
        var room = _roomStore.CreateRoom();
        
        _logger.LogInformation("Room created with code {RoomCode}", room.Code);

        var response = new CreateRoomResponseDto(
            RoomCode: room.Code,
            JoinUrl: $"/join/{room.Code}"
        );

        return CreatedAtAction(nameof(GetRoom), new { code = room.Code }, response);
    }

    /// <summary>
    /// Gets the current state of a room.
    /// </summary>
    /// <param name="code">The room code.</param>
    /// <returns>The room state.</returns>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(RoomStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status404NotFound)]
    public ActionResult<RoomStateDto> GetRoom(string code)
    {
        if (!_roomStore.TryGetRoom(code, out var room) || room == null)
        {
            _logger.LogWarning("Room not found: {RoomCode}", code);
            return NotFound(new ErrorDto("ROOM_NOT_FOUND", $"Room with code '{code}' does not exist."));
        }

        var players = room.Players.Values
            .Select(p => new PlayerDto(
                p.PlayerId, 
                p.DisplayName, 
                p.IsConnected, 
                p.Score,
                p.AvatarPresetId,
                p.AvatarUrl,
                p.AvatarKind
            ))
            .ToList();

        var response = new RoomStateDto(
            RoomCode: room.Code,
            Status: room.Status,
            IsLocked: room.IsLocked,
            Players: players
        );

        return Ok(response);
    }
}
