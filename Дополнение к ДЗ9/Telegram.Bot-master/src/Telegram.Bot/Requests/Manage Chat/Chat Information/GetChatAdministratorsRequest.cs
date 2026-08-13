// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Requests;

/// <summary>Use this method to get a list of administrators in a chat.<para>Returns: An Array of <see cref="ChatMember"/> objects.</para></summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class GetChatAdministratorsRequest() : RequestBase<ChatMember[]>("getChatAdministrators"), IChatTargetable
{
    /// <summary>Unique identifier for the target chat or username of the target supergroup or channel in the format <c>@username</c></summary>
    [JsonPropertyName("chat_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required ChatId ChatId { get; set; }

    /// <summary>Pass <see langword="true"/> to additionally receive all bots that are administrators of the chat. By default, bots other than the current bot are omitted.</summary>
    [JsonPropertyName("return_bots")]
    public bool ReturnBots { get; set; }
}
