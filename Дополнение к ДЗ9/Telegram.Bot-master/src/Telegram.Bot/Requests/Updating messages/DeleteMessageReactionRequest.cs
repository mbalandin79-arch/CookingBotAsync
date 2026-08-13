// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Requests;

/// <summary>Use this method to remove a reaction from a message in a group or a supergroup chat. The bot must have the 'CanDeleteMessages' administrator right in the chat.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class DeleteMessageReactionRequest() : RequestBase<bool>("deleteMessageReaction"), IChatTargetable
{
    /// <summary>Unique identifier for the target chat or username of the target supergroup in the format <c>@username</c></summary>
    [JsonPropertyName("chat_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required ChatId ChatId { get; set; }

    /// <summary>Identifier of the target message</summary>
    [JsonPropertyName("message_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int MessageId { get; set; }

    /// <summary>Identifier of the user whose reaction will be removed, if the reaction was added by a user</summary>
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    /// <summary>Identifier of the chat whose reaction will be removed, if the reaction was added by a chat</summary>
    [JsonPropertyName("actor_chat_id")]
    public long? ActorChatId { get; set; }
}
