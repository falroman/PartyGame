using Microsoft.Extensions.Options;
using PartyGame.Server.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PartyGame.Server.Services;

/// <summary>
/// Service for handling avatar uploads and image processing.
/// </summary>
public interface IAvatarUploadService
{
    /// <summary>
    /// Upload and process an avatar image.
    /// </summary>
    /// <param name="roomCode">The room code (normalized)</param>
    /// <param name="playerId">The player ID (GUID)</param>
    /// <param name="fileStream">The uploaded file stream</param>
    /// <param name="contentType">The MIME type of the uploaded file</param>
    /// <param name="fileSize">The size of the uploaded file in bytes</param>
    /// <returns>The public URL of the uploaded avatar</returns>
    Task<string> UploadAvatarAsync(
        string roomCode, 
        Guid playerId, 
        Stream fileStream, 
        string contentType, 
        long fileSize);

    /// <summary>
    /// Delete a player's avatar.
    /// </summary>
    Task DeleteAvatarAsync(string roomCode, Guid playerId);

    /// <summary>
    /// Delete all avatars for a room.
    /// </summary>
    Task DeleteRoomAvatarsAsync(string roomCode);
}

public class AvatarUploadService : IAvatarUploadService
{
    private readonly AvatarUploadOptions _options;
    private readonly ILogger<AvatarUploadService> _logger;
    private readonly string _uploadRoot;

    public AvatarUploadService(
        IOptions<AvatarUploadOptions> options,
        IWebHostEnvironment environment,
        ILogger<AvatarUploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        // Ensure upload root path ends with directory separator for proper validation
        _uploadRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.UploadFolder));
        
        // Ensure upload directory exists
        Directory.CreateDirectory(_uploadRoot);
        
        _logger.LogInformation("Avatar upload service initialized with root: {UploadRoot}", _uploadRoot);
    }

    public async Task<string> UploadAvatarAsync(
        string roomCode, 
        Guid playerId, 
        Stream fileStream, 
        string contentType, 
        long fileSize)
    {
        // Validate file size
        if (fileSize > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size ({fileSize} bytes) exceeds maximum allowed size ({_options.MaxFileSizeBytes} bytes)");
        }

        // Validate MIME type
        if (!_options.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"File type '{contentType}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedMimeTypes)}");
        }

        // Normalize room code
        roomCode = roomCode.Trim().ToUpperInvariant();

        // Create room-specific directory
        var roomDir = Path.Combine(_uploadRoot, roomCode);
        Directory.CreateDirectory(roomDir);

        // Generate safe filename
        var fileName = $"{playerId}.jpg";
        var filePath = Path.Combine(roomDir, fileName);

        // Ensure path is safe (prevent path traversal)
        // Normalize both paths to handle Windows vs Unix path separators
        var fullPath = Path.GetFullPath(filePath);
        var normalizedUploadRoot = Path.GetFullPath(_uploadRoot);
        
        _logger.LogDebug(
            "Path validation: fullPath={FullPath}, uploadRoot={UploadRoot}", 
            fullPath, normalizedUploadRoot);
        
        if (!fullPath.StartsWith(normalizedUploadRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Path traversal attempt detected: fullPath={FullPath} does not start with uploadRoot={UploadRoot}",
                fullPath, normalizedUploadRoot);
            throw new InvalidOperationException("Invalid file path");
        }

        try
        {
            // Load image from stream
            using var image = await Image.LoadAsync(fileStream);

            // Resize to thumbnail
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_options.ThumbnailSize, _options.ThumbnailSize),
                Mode = ResizeMode.Crop
            }));

            // Save as JPEG
            var encoder = new JpegEncoder
            {
                Quality = _options.JpegQuality
            };

            await image.SaveAsync(fullPath, encoder);

            _logger.LogInformation(
                "Avatar uploaded for player {PlayerId} in room {RoomCode}: {FilePath}",
                playerId, roomCode, fullPath);

            // Return public URL with cache-busting timestamp
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return $"/uploads/avatars/{roomCode}/{fileName}?ts={timestamp}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process avatar upload for player {PlayerId} in room {RoomCode}",
                playerId, roomCode);
            throw new InvalidOperationException("Failed to process image", ex);
        }
    }

    public Task DeleteAvatarAsync(string roomCode, Guid playerId)
    {
        roomCode = roomCode.Trim().ToUpperInvariant();
        var fileName = $"{playerId}.jpg";
        var filePath = Path.Combine(_uploadRoot, roomCode, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation(
                "Deleted avatar for player {PlayerId} in room {RoomCode}",
                playerId, roomCode);
        }

        return Task.CompletedTask;
    }

    public Task DeleteRoomAvatarsAsync(string roomCode)
    {
        roomCode = roomCode.Trim().ToUpperInvariant();
        var roomDir = Path.Combine(_uploadRoot, roomCode);

        if (Directory.Exists(roomDir))
        {
            Directory.Delete(roomDir, recursive: true);
            _logger.LogInformation("Deleted all avatars for room {RoomCode}", roomCode);
        }

        return Task.CompletedTask;
    }
}
