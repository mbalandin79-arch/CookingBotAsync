// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Requests;

/// <summary>Use this method to remove up to 10000 recent reactions in a group or a supergroup chat added by a given user or chat. The bot must have the 'CanDeleteMessages' administrator right in the chat.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class DeleteAllMessageReactionsRequest() : RequestBase<bool>("deleteAllMessageReactions"), IChatTargetable
{
    /// <summary>Unique identifier for the target chat or username of the target supergroup in the format <c>@username</c></summary>
    [JsonPropertyName("chat_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required ChatId ChatId { get; set; }

    /// <summary>Identifier of the user whose reactions will be removed, if the reactions were added by a user</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Identifier of the chat whose reactions will be removed, if the reactions were added by a chat</summary>
    [JsonPropertyName("actor_chat_id")]
    public long? ActorChatId { get; set; }
}
