// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types;

/// <summary>This object represents the content of a poll description or a quiz explanation to be sent. It should be one of<br/><see cref="InputMediaAnimation"/>, <see cref="InputMediaAudio"/>, <see cref="InputMediaDocument"/>, <see cref="InputMediaLivePhoto"/>, <see cref="InputMediaLocation"/>, <see cref="InputMediaPhoto"/>, <see cref="InputMediaVenue"/>, <see cref="InputMediaVideo"/></summary>
[JsonConverter(typeof(PolymorphicJsonConverter<InputPollMedia>))]
[CustomJsonPolymorphic("type")]
[CustomJsonDerivedType(typeof(InputMediaAnimation), "animation")]
[CustomJsonDerivedType(typeof(InputMediaAudio), "audio")]
[CustomJsonDerivedType(typeof(InputMediaDocument), "document")]
[CustomJsonDerivedType(typeof(InputMediaLivePhoto), "live_photo")]
[CustomJsonDerivedType(typeof(InputMediaLocation), "location")]
[CustomJsonDerivedType(typeof(InputMediaPhoto), "photo")]
[CustomJsonDerivedType(typeof(InputMediaVenue), "venue")]
[CustomJsonDerivedType(typeof(InputMediaVideo), "video")]
#pragma warning disable CA1715, IDE1006
public interface InputPollMedia
{
    /// <summary>Type of the media</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public abstract InputMediaType Type { get; }
}
