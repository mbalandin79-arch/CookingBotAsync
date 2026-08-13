// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types.Enums;

/// <summary>Type of the paid media</summary>
[JsonConverter(typeof(EnumConverter<PaidMediaType>))]
public enum PaidMediaType
{
    /// <summary>The paid media isn't available before the payment.<br/><br/><i>(<see cref="PaidMedia"/> can be cast into <see cref="PaidMediaPreview"/>)</i></summary>
    Preview = 1,
    /// <summary>The paid media is a photo.<br/><br/><i>(<see cref="PaidMedia"/> can be cast into <see cref="PaidMediaPhoto"/>)</i></summary>
    Photo,
    /// <summary>The paid media is a video.<br/><br/><i>(<see cref="PaidMedia"/> can be cast into <see cref="PaidMediaVideo"/>)</i></summary>
    Video,
    /// <summary>The paid media is a <see cref="LivePhoto">live photo</see>.<br/><br/><i>(<see cref="PaidMedia"/> can be cast into <see cref="PaidMediaLivePhoto"/>)</i></summary>
    LivePhoto,
}
