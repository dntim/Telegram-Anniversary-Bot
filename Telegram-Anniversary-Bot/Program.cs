using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramAnniversaryBot.Models;

namespace TelegramAnniversaryBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
                   Host.CreateDefaultBuilder(args)
                       .ConfigureServices((context, services) =>
                       {
                           // Retrieve the connection string from Azure Key Vault
                           var keyVaultUrl = "https://telegram-bots.vault.azure.net/";
                           var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

                           // Get SQL connection string secret from Azure Key Vault
                           var connectionStringSecret = secretClient.GetSecret("anniversary-reminder-bot-sql-connection-string").Value.Value;

                           // Register the DbContext with the retrieved connection string
                           services.AddDbContext<AnniversaryReminderBotDbContext>(options =>
                               options.UseSqlServer(connectionStringSecret));

                           // Get Telegram bot token from Azure Key Vault
                           var botTokenSecret = secretClient.GetSecret("anniversary-reminder-bot-token").Value.Value;

                           // Register TelegramBotClient as a singleton
                           services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botTokenSecret));

                           // Register BotService as a hosted service
                           services.AddHostedService<BotService>();
                       });
    }
}
