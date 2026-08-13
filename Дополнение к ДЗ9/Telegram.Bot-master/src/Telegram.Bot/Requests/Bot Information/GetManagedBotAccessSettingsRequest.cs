// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Requests;

/// <summary>Use this method to get the access settings of a managed bot.<para>Returns: A <see cref="BotAccessSettings"/> object on success.</para></summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class GetManagedBotAccessSettingsRequest() : RequestBase<BotAccessSettings>("getManagedBotAccessSettings"), IUserTargetable
{
    /// <summary>User identifier of the managed bot whose access settings will be returned</summary>
    [JsonPropertyName("user_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required long UserId { get; set; }
}
