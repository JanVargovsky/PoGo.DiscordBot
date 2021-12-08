using System.Linq;
using Discord;

namespace PoGo.DiscordBot;

internal static class UnicodeEmojis
{
    public const string ThumbsUp = "👍";
    public const string ThumbsDown = "👎";
    public const string Check = "✅";
    public const string Cross = "❌";
    public const string NoPedestrians = "🚷";
    public const string Handshake = "🤝";

    public static readonly string[] KeycapDigits;

    static UnicodeEmojis()
    {
        KeycapDigits = new[]
        {
                "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣"
            };
    }
}

internal static class Emojis
{
    public static readonly Emoji ThumbsUp = new(UnicodeEmojis.ThumbsUp);
    public static readonly Emoji ThumbsDown = new(UnicodeEmojis.ThumbsDown);
    public static readonly Emoji Check = new(UnicodeEmojis.Check);
    public static readonly Emoji Cross = new(UnicodeEmojis.Cross);
    public static readonly Emoji NoPedestrians = new(UnicodeEmojis.NoPedestrians);
    public static readonly Emoji Handshake = new(UnicodeEmojis.Handshake);

    public static readonly Emoji[] KeycapDigits;

    static Emojis()
    {
        KeycapDigits = UnicodeEmojis.KeycapDigits.Select(t => new Emoji(t)).ToArray();
    }
}
