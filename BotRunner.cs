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

            var control = new ConsoleBotController(!config.SkipConsoleBotCreation, bot.RunAsync,
                () => bot.Connection.DisconnectIfConnected(), () => config.AcceptingCommands = false,
                () => OrderStatusStore.Shared.HasActiveOrder,
                ex => bot.Log($"Console loop failed: {ex.Message}. Check the connection and use Start bot in the control panel."));
            Globals.ConsoleControl = control;

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancel);

            if (!hasDiscordToken && !hasTwitchToken && !hasSignalr && !hasWebApi)
                bot.Log("Warning: No communication service is configured (Discord/Twitch/SignalR/WebAPI). The bot will run in console-only mode.");
            else
                bot.Log($"Active services:{(hasDiscordToken ? " Discord" : "")}{(hasTwitchToken ? " Twitch" : "")}{(hasSignalr ? " SignalR" : "")}{(hasWebApi ? " WebAPI" : "")}");

            var consoleTask = control.RunAsync(lifetime.Token);

            try
            {
                if (hasTwitchToken)
                {
                    bot.Log("Starting Twitch.");
                    _ = new TwitchCrossBot(tConfig!, bot);
                }

                if (hasSignalr)
                {
                    bot.Log("Starting Web (SignalR).");
                    _ = new SignalrCrossBot(config.SignalrConfig, bot);
                }

                if (hasDiscordToken)
                {
                    while (!lifetime.IsCancellationRequested)
                    {
                        try
                        {
                            bot.Log("Starting Discord.");
                            await sys.MainAsync(config.Token, lifetime.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                        catch (InvalidOperationException ex)
                        {
                            bot.Log($"Discord configuration is invalid: {ex.Message}. Automatic restart is disabled.");
                            break;
                        }
                        catch (Exception ex) 
                        { 
                            bot.Log($"Discord failed: {ex.Message}. Attempting to reconnect in 10 seconds."); 
                        }
                        
                        if (!lifetime.IsCancellationRequested)
                        {
                            await sys.Disconnect().ConfigureAwait(false);
                            await Task.Delay(10_000, lifetime.Token).ConfigureAwait(false);
                            sys = new SysCord(bot);
                            Globals.Self = sys;
                        }
                    }
                }
                else
                {
                    // No Discord, just wait for console/cancel task
                    await consoleTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            finally
            {
                lifetime.Cancel();
                await consoleTask.ConfigureAwait(false);
                await sys.Disconnect().ConfigureAwait(false);
                LogUtil.Forwarders.Remove(Logger);
            }
        }
    }
}
