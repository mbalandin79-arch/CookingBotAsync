// GENERATED FILE - DO NOT MODIFY MANUALLY
namespace Telegram.Bot.Types;

/// <summary>This object describes the access settings of a bot.</summary>
public partial class BotAccessSettings
{
    /// <summary><see langword="true"/>, if only selected users can access the bot. The bot's owner can always access it.</summary>
    [JsonPropertyName("is_access_restricted")]
    public bool IsAccessRestricted { get; set; }

    /// <summary><em>Optional</em>. The list of other users who have access to the bot if the access is restricted</summary>
    [JsonPropertyName("added_users")]
    public User[]? AddedUsers { get; set; }
}
