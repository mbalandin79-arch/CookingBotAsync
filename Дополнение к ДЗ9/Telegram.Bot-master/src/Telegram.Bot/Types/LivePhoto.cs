// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types;

/// <summary>This object represents a live photo.</summary>
public partial class LivePhoto : FileBase
{
    /// <summary><em>Optional</em>. Available sizes of the corresponding static photo</summary>
    public PhotoSize[]? Photo { get; set; }

    /// <summary>Video width as defined by the sender</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int Width { get; set; }

    /// <summary>Video height as defined by the sender</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int Height { get; set; }

    /// <summary>Duration of the video in seconds as defined by the sender</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int Duration { get; set; }

    /// <summary><em>Optional</em>. MIME type of the file as defined by the sender</summary>
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }
}
