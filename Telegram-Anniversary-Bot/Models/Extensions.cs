namespace TelegramAnniversaryBot.Models
{
    public static class Extensions
    {
        public static string EncodeForHtmlMarkup(this string source)
        {
            return source
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("&", "&amp;");
        }
    }
}