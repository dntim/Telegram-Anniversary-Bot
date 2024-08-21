using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.DependencyInjection;
using TelegramAnniversaryBot.Models;
using Microsoft.EntityFrameworkCore;

namespace TelegramAnniversaryBot
{
    public class BotService : IHostedService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly AnniversaryReminderBotDbContext _dbContext;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<long> _adminChatIDs = [78502881, 72807];
        private bool _turnOff = false;
        private DateTime _lastDbCheckedDT = DateTime.MinValue;
        private readonly int _checkDbIntervalInMinutes = 30;
        private List<AnniversaryReminderBotEvent> _todaysEvents;

        public BotService(ITelegramBotClient botClient, AnniversaryReminderBotDbContext dbContext)
        {
            _botClient = botClient;
            _dbContext = dbContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var me = await _botClient.GetMeAsync(cancellationToken);
            await _botClient.DropPendingUpdatesAsync();
            Console.WriteLine($"@{me.Username} is running...");

            // Start background task for checking and sending updates
            _ = Task.Run(() => CheckAndSendNotificationsAsync(cancellationToken));

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions
                {
                    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
                },
                cancellationToken: _cts.Token
            );
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _turnOff = true;
            _cts.Cancel(); // stop the bot
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                var message = update.Message;
                Console.WriteLine($"Received message of type {message.Type} from {message.Chat.Id}");
                await OnMessage(message, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await OnCallbackQuery(update.CallbackQuery, cancellationToken);
            }
            else if (update.Type == UpdateType.PollAnswer && update.PollAnswer != null)
            {
                await OnPollAnswer(update.PollAnswer, cancellationToken);
            }
        }

        private async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(exception);
            await Task.Delay(2000, cancellationToken); // simple delay before retrying
        }

        private async Task OnMessage(Message msg, CancellationToken cancellationToken)
        {
            if (msg.Text != null && msg.Text.StartsWith('/'))
            {
                var space = msg.Text.IndexOf(' ');
                if (space < 0) space = msg.Text.Length;
                var command = msg.Text[..space].ToLower();
                if (command.LastIndexOf('@') is > 0 and int at) // it's a targeted command
                    if (command[(at + 1)..].Equals((await _botClient.GetMeAsync(cancellationToken)).Username, StringComparison.OrdinalIgnoreCase))
                        command = command[..at];
                    else
                        return; // command was not targeted at me
                await OnCommand(command, msg.Text[space..].TrimStart(), msg, cancellationToken);
            }
            else
            { // Here placeholder for answering 
                string response = GenerateRandomResponse;
                await _botClient.SendTextMessageAsync(msg.Chat.Id, response, cancellationToken: cancellationToken);
            }
        }

        private async Task OnCommand(string command, string args, Message msg, CancellationToken cancellationToken)
        {
            switch (command)
            {
                case "/send":
                case "/send@AnniversaryReminderBot":
                    await SendAnniversaryNotificationsAsync(UpdateTodaysEventsFromTheDatabase(), cancellationToken);
                    break;

                case "/chatid":
                case "/chatid@AnniversaryReminderBot":
                    await _botClient.SendTextMessageAsync(msg.Chat.Id, $"This chat ID is: {msg.Chat.Id}", replyParameters: new() { MessageId = msg.MessageId }, cancellationToken: cancellationToken);
                    break;

                case "/whoami":
                case "/whoami@AnniversaryReminderBot":
                    await _botClient.SendTextMessageAsync(msg.Chat.Id,
                        $"ID: {msg.From.Id}{Environment.NewLine}Username: {msg.From.Username}{Environment.NewLine}First name: {msg.From.FirstName}{Environment.NewLine}Last name: {msg.From.LastName}{Environment.NewLine}Language code: {msg.From.LanguageCode}",
                        replyParameters: new() { MessageId = msg.MessageId },
                        cancellationToken: cancellationToken);
                    break;

                case "/kill":
                case "/kill@AnniversaryReminderBot":
                    if (_adminChatIDs.Contains(msg.From.Id))
                    {
                        await _botClient.SendTextMessageAsync(msg.Chat.Id, "ok, stopping the bot...", replyParameters: new() { MessageId = msg.MessageId }, cancellationToken: cancellationToken);
                        _turnOff = true;
                    }
                    break;

                default:
                    string response = GenerateRandomResponse;
                    await _botClient.SendTextMessageAsync(msg.Chat.Id, response, cancellationToken: cancellationToken);
                    break;
            }
        }

        private async Task OnCallbackQuery(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"You selected {callbackQuery.Data}", cancellationToken: cancellationToken);
            await _botClient.SendTextMessageAsync(callbackQuery.Message!.Chat.Id, $"Received callback from inline button {callbackQuery.Data}", cancellationToken: cancellationToken);
        }

        private async Task OnPollAnswer(PollAnswer pollAnswer, CancellationToken cancellationToken)
        {
            if (pollAnswer.User != null)
                await _botClient.SendTextMessageAsync(pollAnswer.User.Id, $"You voted for option(s) id [{string.Join(',', pollAnswer.OptionIds)}]", cancellationToken: cancellationToken);
        }

        private static string GenerateRandomResponse
        {
            get
            {
                Random rnd = new();
                return rnd.NextDouble() < 0.1d ? "атата!" : "бебебе";
            }
        }

        private async Task CheckAndSendNotificationsAsync(CancellationToken cancellationToken)
        {
            await UpdateTodaysEventsFromTheDatabase();

            while (!_turnOff)
            {
                await Task.Delay(20000, cancellationToken);

                // Need to update from the database?
                if (DateTime.Now.Subtract(_lastDbCheckedDT).TotalMinutes >= _checkDbIntervalInMinutes)
                {
                    await UpdateTodaysEventsFromTheDatabase();
                }

                // Check if anything needs to be sent right now
                await SendAnniversaryNotificationsAsync(_todaysEvents, cancellationToken);
            }
        }

        private async Task UpdateTodaysEventsFromTheDatabase()
        {
            _lastDbCheckedDT = DateTime.Now;
            var utcDate = DateTime.UtcNow.Date;

            _todaysEvents = await _dbContext.AnniversaryReminderBotEvents.Where(date =>
                date.EventDate.Month == utcDate.Month
                && date.EventDate.Day == utcDate.Day
                && date.DateTimeLastCongratulated != null
                && date.DateTimeLastCongratulated.Value.Date != utcDate).ToListAsync();
        }

        private async Task SendAnniversaryNotificationsAsync(List<AnniversaryReminderBotEvent> todaysEvents, CancellationToken cancellationToken)
        {
            try
            {
                bool dbUpdated = false;
                foreach (var a in todaysEvents)
                {
                    try
                    {
                        if (DateTime.UtcNow.TimeOfDay < a.CongratsAtTime.ToTimeSpan()) continue;

                        var allCongratsIDs = a.CongratsTargetIds.Split(',', ';').Select(c => c.Trim());
                        List<string> allCongratsUsernames = new();
                        string targetUsernamesString = "";
                        try
                        {
                            foreach (var id in allCongratsIDs)
                            {
                                var chatMember = await _botClient.GetChatMemberAsync(a.NotifyChatId, Convert.ToInt32(id), cancellationToken);
                                if (!string.IsNullOrWhiteSpace(chatMember.User.Username))
                                    allCongratsUsernames.Add("@" + chatMember.User.Username);
                                else
                                {
                                    var fullName = $"{chatMember.User.FirstName} {chatMember.User.LastName}";
                                    allCongratsUsernames.Add($"<a href=\"tg://user?id={chatMember.User.Id}\">{fullName}</a>");
                                }
                            }
                            targetUsernamesString = string.Join(", ", allCongratsUsernames);
                        }
                        catch (Exception)
                        {
                            targetUsernamesString = a.CongratsAlternativeNames;
                        }

                        var msg = $"Поздравляем {targetUsernamesString} c {a.Event.EncodeForHtmlMarkup()}!";
                        await _botClient.SendTextMessageAsync(
                         chatId: a.NotifyChatId,
                         text: msg,
                         parseMode: ParseMode.Html,
                         cancellationToken: cancellationToken);

                        a.DateTimeLastCongratulated = DateTime.UtcNow;
                        dbUpdated = true;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            foreach (var ac in _adminChatIDs)
                            {
                                await _botClient.SendTextMessageAsync(ac, $"<b>Error has occurred:</b>{Environment.NewLine}{Environment.NewLine}Target IDs: {a.CongratsTargetIds}{Environment.NewLine}Event: {a.Event.EncodeForHtmlMarkup()}{Environment.NewLine}Event date: {a.EventDate}{Environment.NewLine}{Environment.NewLine}<code>{ex.ToString().EncodeForHtmlMarkup()}</code>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                            }
                        }
                        catch { }
                    }
                }

                if (dbUpdated)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await UpdateTodaysEventsFromTheDatabase();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    foreach (var ac in _adminChatIDs)
                    {
                        await _botClient.SendTextMessageAsync(ac, $"<b>Error has occurred:</b>{Environment.NewLine}{Environment.NewLine}<code>{ex.ToString().EncodeForHtmlMarkup()}</code>", parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                    }
                }
                catch { }
            }
        }
    }
}
