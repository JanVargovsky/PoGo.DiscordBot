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

        const char Border = '⃣';
        public static readonly string[] KeycapDigits;

        static UnicodeEmojis()
        {
            KeycapDigits = Enumerable.Range(1, 9)
                .Select(t => new string(new char[] { (char)(t + '0'), Border }))
                .ToArray();
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
