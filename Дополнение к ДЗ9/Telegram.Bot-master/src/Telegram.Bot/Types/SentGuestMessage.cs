// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types;

/// <summary>Describes an inline message sent by a guest bot.</summary>
public partial class SentGuestMessage
{
    /// <summary>Identifier of the sent inline message</summary>
    [JsonPropertyName("inline_message_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string InlineMessageId { get; set; } = default!;

    /// <summary>Implicit conversion to string (InlineMessageId)</summary>
    public static implicit operator string(SentGuestMessage self) => self.InlineMessageId;
    /// <summary>Implicit conversion from string (InlineMessageId)</summary>
    public static implicit operator SentGuestMessage(string inlineMessageId) => new() { InlineMessageId = inlineMessageId };
}
