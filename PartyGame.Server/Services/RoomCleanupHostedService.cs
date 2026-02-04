using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyGame.Server.Options;

namespace PartyGame.Server.Services;

/// <summary>
/// Background service that periodically cleans up stale rooms and disconnected players.
/// 
/// Design decision: Uses ILobbyService for cleanup operations to maintain single responsibility
/// and ensure consistent broadcast behavior. The LobbyService already has IHubContext for
/// broadcasting LobbyUpdated events, so cleanup operations that affect players will
/// automatically notify connected clients.
/// </summary>
public class RoomCleanupHostedService : BackgroundService
{
    private readonly ILobbyService _lobbyService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoomCleanupHostedService> _logger;
    private readonly RoomCleanupOptions _options;

    public RoomCleanupHostedService(
        ILobbyService lobbyService,
        IServiceProvider serviceProvider,
        IOptions<RoomCleanupOptions> options,
        ILogger<RoomCleanupHostedService> logger)
    {
        _lobbyService = lobbyService;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Room cleanup service is disabled");
            return;
        }

        _logger.LogInformation(
            "Room cleanup service started. Interval: {Interval}s, Room TTL: {RoomTtl}m, Player grace: {PlayerGrace}s",
            _options.CleanupIntervalSeconds,
            _options.RoomWithoutHostTtlMinutes,
            _options.DisconnectedPlayerGraceSeconds);

        var interval = TimeSpan.FromSeconds(_options.CleanupIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RunCleanupAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during room cleanup");
            }
        }

        _logger.LogInformation("Room cleanup service stopped");
    }

    /// <summary>
    /// Runs a single cleanup cycle.
    /// Exposed for testing purposes.
    /// </summary>
    public async Task RunCleanupAsync()
    {
        var roomTtl = TimeSpan.FromMinutes(_options.RoomWithoutHostTtlMinutes);
        var playerGrace = TimeSpan.FromSeconds(_options.DisconnectedPlayerGraceSeconds);

        // Get rooms to cleanup before removing players (to avoid modifying collection during iteration)
        var hostlessRooms = _lobbyService.GetHostlessRoomsForCleanup(roomTtl);

        // Remove hostless rooms
        foreach (var roomCode in hostlessRooms)
        {
            _lobbyService.RemoveRoom(roomCode);
        }

        if (hostlessRooms.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} hostless room(s): {RoomCodes}",
                hostlessRooms.Count, string.Join(", ", hostlessRooms));
        }

        // Remove disconnected players from remaining rooms
        // We need to get fresh room list after removing hostless rooms
        var allRoomCodes = GetAllRoomCodes();
        var totalPlayersRemoved = 0;

        foreach (var roomCode in allRoomCodes)
        {
            var removed = await _lobbyService.RemoveDisconnectedPlayersAsync(roomCode, playerGrace);
            totalPlayersRemoved += removed;
        }

        if (totalPlayersRemoved > 0)
        {
            _logger.LogInformation("Cleaned up {Count} disconnected player(s) total", totalPlayersRemoved);
        }
    }

    private IReadOnlyList<string> GetAllRoomCodes()
    {
        // Get room store from service provider to get all rooms
        // This is a bit awkward but avoids circular dependencies
        using var scope = _serviceProvider.CreateScope();
        var roomStore = scope.ServiceProvider.GetRequiredService<PartyGame.Core.Interfaces.IRoomStore>();
        return roomStore.GetAll().Select(r => r.Code).ToList();
    }
}
