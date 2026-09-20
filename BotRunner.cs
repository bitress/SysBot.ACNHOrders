using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using SysBot.ACNHOrders.Twitch;
using SysBot.ACNHOrders.Signalr;

namespace SysBot.ACNHOrders
{
    public static class BotRunner
    {
        public static async Task RunFrom(CrossBotConfig config, CancellationToken cancel, TwitchConfig? tConfig = null, SocketAPI.SocketAPIServerConfig? serverConfig = null)
        {
            // Set up logging for Console Window
            LogUtil.Forwarders.Add(Logger);
            static void Logger(string msg, string identity) => Console.WriteLine(GetMessage(msg, identity));
            static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";

            var bot = new CrossBot(config);

            var sys = new SysCord(bot);

            Globals.Self = sys;
            Globals.Bot = bot;
            Globals.Hub = QueueHub.CurrentInstance;
            GlobalBan.UpdateConfiguration(config);

            bool hasDiscordToken = !string.IsNullOrWhiteSpace(config.Token) && config.Token != "DISCORD_TOKEN";
            bool hasTwitchToken  = tConfig != null && !string.IsNullOrWhiteSpace(tConfig.Token);
            bool hasSignalr      = !string.IsNullOrWhiteSpace(config.SignalrConfig.URIEndpoint);
            bool hasWebApi       = serverConfig?.HttpApiEnabled == true;

            if (hasDiscordToken)
            {
                bot.Log("Starting Discord.");
#pragma warning disable 4014
                Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014
            }
            else
            {
                bot.Log("Discord token is not configured — skipping Discord.");
            }

            if (hasTwitchToken)
            {
                bot.Log("Starting Twitch.");
                var _ = new TwitchCrossBot(tConfig!, bot);
            }

            if (hasSignalr)
            {
                bot.Log("Starting Web (SignalR).");
                var _ = new SignalrCrossBot(config.SignalrConfig, bot);
            }

            if (!hasDiscordToken && !hasTwitchToken && !hasSignalr && !hasWebApi)
                bot.Log("Warning: No communication service is configured (Discord/Twitch/SignalR/WebAPI). The bot will run in console-only mode.");
            else
                bot.Log($"Active services:{(hasDiscordToken ? " Discord" : "")}{(hasTwitchToken ? " Twitch" : "")}{(hasSignalr ? " SignalR" : "")}{(hasWebApi ? " WebAPI" : "")}");

            if (config.SkipConsoleBotCreation)
            {
                await Task.Delay(-1, cancel).ConfigureAwait(false);
                return;
            }

            while (!cancel.IsCancellationRequested)
            {
                bot.Log("Starting bot loop.");

                var task = bot.RunAsync(cancel);
                await task.ConfigureAwait(false);

                bool attemptReconnect = false;

                if (task.IsFaulted)
                {
                    if (task.Exception == null)
                    {
                        bot.Log("Bot has terminated due to an unknown error.");
                    }
                    else
                    {
                        bot.Log("Bot has terminated due to an error:");
                        foreach (var ex in task.Exception.InnerExceptions)
                        {
                            bot.Log(ex.Message);
                            var st = ex.StackTrace;
                            if (st != null)
                                bot.Log(st);
                        }
                    }
                    attemptReconnect = false;
                }
                else
                {
                    bot.Log("Bot has terminated.");
                   // if (config.DodoModeConfig.LimitedDodoRestoreOnlyMode) // don't restore ordermode crashes
                   // {
                        attemptReconnect = true;
                        bot.Log("Please wait... Attempting to reconnect in 10 seconds.");
                   // }
                }

                if (attemptReconnect)
                {
                    await Task.Delay(10_000, cancel).ConfigureAwait(false);
                    bot.Log("Bot is attempting a restart...");
                    bot = new CrossBot(config);
                    Globals.Bot = bot;

                    if (hasDiscordToken)
                    {
                        await sys.Disconnect();
                        sys = new SysCord(bot);
                        Globals.Self = sys;
                        bot.Log("Restarting Discord.");
#pragma warning disable 4014
                        Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014
                    }
                }
                else
                    break;
            }
        }
    }
}
