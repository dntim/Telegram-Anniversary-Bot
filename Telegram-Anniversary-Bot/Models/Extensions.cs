using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;

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

        public static async Task NotifyAdmins(this ITelegramBotClient botClient, string message, CancellationToken cancellationToken, params long[] adminChatIDs)
        {
            foreach (var adminChatID in adminChatIDs)
            {
                try
                {
                    await botClient.SendTextMessageAsync(adminChatID, message, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                    Console.WriteLine($"Notified admin with ID={adminChatID}: {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error message while trying to notify admin with ID={adminChatID}: {ex.Message}");
                }
            }
        }
    }
}