// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Requests;

/// <summary>Use this method to reply to a received guest message.<para>Returns: A <see cref="SentGuestMessage"/> object is returned.</para></summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class AnswerGuestQueryRequest() : RequestBase<SentGuestMessage>("answerGuestQuery")
{
    /// <summary>Unique identifier for the query to be answered</summary>
    [JsonPropertyName("guest_query_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string GuestQueryId { get; set; }

    /// <summary>An object describing the message to be sent</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required InlineQueryResult Result { get; set; }
}
