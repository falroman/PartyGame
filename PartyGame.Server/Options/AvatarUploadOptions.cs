namespace PartyGame.Server.Options;

/// <summary>
/// Configuration options for avatar upload functionality.
/// </summary>
public class AvatarUploadOptions
{
    public const string SectionName = "AvatarUpload";

    /// <summary>
    /// Maximum allowed file size in bytes (default: 2MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Allowed MIME types for uploaded images.
    /// </summary>
    public List<string> AllowedMimeTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Thumbnail size (width and height in pixels).
    /// </summary>
    public int ThumbnailSize { get; set; } = 128;

    /// <summary>
    /// JPEG quality for thumbnails (1-100).
    /// </summary>
    public int JpegQuality { get; set; } = 80;

    /// <summary>
    /// Upload folder path relative to content root.
    /// </summary>
    public string UploadFolder { get; set; } = "App_Data/uploads/avatars";
}
