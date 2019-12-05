using Discord;
using System.Linq;

namespace PoGo.DiscordBot
{
    internal class UnicodeEmojis
    {
        public const string ThumbsUp = "👍";
        public const string ThumbsDown = "👎";
        public const string Check = "✅";
        public const string Cross = "❌";

        public static readonly string[] KeycapDigits;

        static UnicodeEmojis()
        {
            KeycapDigits = new[]
            {
                "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣"
            };
        }
    }

    internal class Emojis
    {
        public static readonly Emoji ThumbsUp = new Emoji(UnicodeEmojis.ThumbsUp);
        public static readonly Emoji ThumbsDown = new Emoji(UnicodeEmojis.ThumbsDown);
        public static readonly Emoji Check = new Emoji(UnicodeEmojis.Check);
        public static readonly Emoji Cross = new Emoji(UnicodeEmojis.Cross);

        public static readonly Emoji[] KeycapDigits;

        static Emojis()
        {
            KeycapDigits = UnicodeEmojis.KeycapDigits.Select(t => new Emoji(t)).ToArray();
        }
    }
}
