// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types.Enums;

/// <summary>Type of the media</summary>
[JsonConverter(typeof(EnumConverter<InputPollMediaType>))]
public enum InputPollMediaType
{
    /// <summary>Represents an animation file (GIF or H.264/MPEG-4 AVC video without sound) to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaAnimation"/>)</i></summary>
    Animation = 1,
    /// <summary>Represents an audio file to be treated as music to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaAudio"/>)</i></summary>
    Audio,
    /// <summary>Represents a general file to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaDocument"/>)</i></summary>
    Document,
    /// <summary>Represents a live photo to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaLivePhoto"/>)</i></summary>
    LivePhoto,
    /// <summary>Represents a location to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaLocation"/>)</i></summary>
    Location,
    /// <summary>Represents a photo to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaPhoto"/>)</i></summary>
    Photo,
    /// <summary>Represents a venue to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaVenue"/>)</i></summary>
    Venue,
    /// <summary>Represents a video to be sent.<br/><br/><i>(<see cref="InputPollMedia"/> can be cast into <see cref="InputMediaVideo"/>)</i></summary>
    Video,
}
