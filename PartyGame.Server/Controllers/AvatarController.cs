using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using PartyGame.Core.Enums;
using PartyGame.Core.Interfaces;
using PartyGame.Server.DTOs;
using PartyGame.Server.Hubs;
using PartyGame.Server.Options;
using PartyGame.Server.Services;

namespace PartyGame.Server.Controllers;

/// <summary>
/// Controller for avatar upload functionality.
/// </summary>
[ApiController]
[Route("api/rooms/{roomCode}/players/{playerId}/avatar")]
public class AvatarController : ControllerBase
{
    private readonly IAvatarUploadService _avatarService;
    private readonly IRoomStore _roomStore;
    private readonly IQuizGameOrchestrator _orchestrator;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly AvatarUploadOptions _options;
    private readonly ILogger<AvatarController> _logger;

    public AvatarController(
        IAvatarUploadService avatarService,
        IRoomStore roomStore,
        IQuizGameOrchestrator orchestrator,
        IHubContext<GameHub> hubContext,
        IOptions<AvatarUploadOptions> options,
        ILogger<AvatarController> logger)
    {
        _avatarService = avatarService;
        _roomStore = roomStore;
        _orchestrator = orchestrator;
        _hubContext = hubContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Upload an avatar for a player.
    /// </summary>
    /// <param name="roomCode">The room code</param>
    /// <param name="playerId">The player ID (GUID)</param>
    /// <param name="file">The uploaded image file</param>
    /// <returns>The avatar URL</returns>
    [HttpPost]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AvatarUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAvatar(
        [FromRoute] string roomCode,
        [FromRoute] Guid playerId,
        [FromForm] IFormFile file)
    {
        // Validate inputs
        var normalizedCode = roomCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length != 4)
        {
            return BadRequest(new ErrorDto(
                "INVALID_ROOM_CODE",
                "Room code must be exactly 4 characters"));
        }

        if (playerId == Guid.Empty)
        {
            return BadRequest(new ErrorDto(
                "INVALID_PLAYER_ID",
                "Player ID is required"));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorDto(
                "NO_FILE",
                "No file uploaded"));
        }

        // Check room exists
        if (!_roomStore.TryGetRoom(normalizedCode, out var room) || room == null)
        {
            return NotFound(new ErrorDto(
                "ROOM_NOT_FOUND",
                $"Room with code '{normalizedCode}' does not exist"));
        }

        // Check player exists in room
        if (!room.Players.ContainsKey(playerId))
        {
            return NotFound(new ErrorDto(
                "PLAYER_NOT_FOUND",
                $"Player with ID '{playerId}' is not in this room"));
        }

        // Validate file size
        if (file.Length > _options.MaxFileSizeBytes)
        {
            return BadRequest(new ErrorDto(
                "AVATAR_TOO_LARGE",
                $"File size ({file.Length} bytes) exceeds maximum allowed size ({_options.MaxFileSizeBytes} bytes)"));
        }

        // Validate MIME type
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!_options.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorDto(
                "AVATAR_INVALID_TYPE",
                $"File type '{contentType}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedMimeTypes)}"));
        }

        try
        {
            // Upload and process avatar
            string avatarUrl;
            using (var stream = file.OpenReadStream())
            {
                avatarUrl = await _avatarService.UploadAvatarAsync(
                    normalizedCode,
                    playerId,
                    stream,
                    contentType,
                    file.Length);
            }

            // Update player state
            var player = room.Players[playerId];
            player.AvatarKind = PartyGame.Core.Models.AvatarKind.Uploaded;
            player.AvatarUrl = avatarUrl;
            player.AvatarPresetId = null; // Clear preset when uploaded

            _logger.LogInformation(
                "Avatar uploaded successfully for player {PlayerId} in room {RoomCode}: {AvatarUrl}",
                playerId, normalizedCode, avatarUrl);

            // Broadcast state update
            if (room.Status == RoomStatus.InGame && room.CurrentGame != null)
            {
                // In-game: Get quiz state from orchestrator and broadcast it
                var dto = _orchestrator.GetStateDto(normalizedCode);
                if (dto != null)
                {
                    await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("QuizStateUpdated", dto);
                }
            }
            else
            {
                // In lobby: broadcast lobby state  
                var lobbyDto = new RoomStateDto(
                    RoomCode: room.Code,
                    Status: room.Status,
                    IsLocked: room.IsLocked,
                    Players: room.Players.Values.Select(p => new PlayerDto(
                        PlayerId: p.PlayerId,
                        DisplayName: p.DisplayName,
                        IsConnected: p.IsConnected,
                        Score: p.Score,
                        AvatarPresetId: p.AvatarPresetId,
                        AvatarUrl: p.AvatarUrl,
                        AvatarKind: p.AvatarKind
                    )).ToList(),
                    CurrentGame: room.CurrentGame != null ? new GameSessionDto(
                        GameType: room.CurrentGame.GameType.ToString(),
                        Phase: room.CurrentGame.Phase?.ToString() ?? "Unknown",
                        StartedUtc: DateTime.UtcNow  // Use current time as fallback
                    ) : null
                );
                
                await _hubContext.Clients.Group($"room:{normalizedCode}").SendAsync("LobbyUpdated", lobbyDto);
            }

            return Ok(new AvatarUploadResponseDto
            {
                AvatarUrl = avatarUrl
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Avatar upload validation failed");
            return BadRequest(new ErrorDto(
                "AVATAR_UPLOAD_FAILED",
                ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Avatar upload failed unexpectedly");
            return StatusCode(500, new ErrorDto(
                "INTERNAL_ERROR",
                "An unexpected error occurred while uploading the avatar"));
        }
    }
}

/// <summary>
/// Response DTO for avatar upload.
/// </summary>
public class AvatarUploadResponseDto
{
    /// <summary>
    /// The public URL of the uploaded avatar (with cache-busting timestamp).
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;
}
