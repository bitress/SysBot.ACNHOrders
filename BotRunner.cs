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

            CancellationTokenSource? discordCancellation = null;
            Task? discordTask = null;

            if (hasDiscordToken)
            {
                bot.Log("Starting Discord.");
                discordCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                discordTask = StartDiscord(sys, config.Token, discordCancellation.Token);
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
                if (discordTask != null)
                {
                    try
                    {
                        await discordTask.WaitAsync(cancel).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                    {
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        if (discordCancellation != null)
                        {
                            discordCancellation.Cancel();
                            await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                            await sys.Disconnect().ConfigureAwait(false);
                            discordCancellation.Dispose();
                        }
                    }
                }
                else
                {
                    try
                    {
                        await Task.Delay(-1, cancel).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                    {
                    }
                }
                return;
            }

            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    bot.Log("Starting bot loop.");
                    using var botCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                    var botTask = bot.RunAsync(botCancellation.Token);

                    Task completed;
                    if (hasDiscordToken && discordTask != null)
                    {
                        completed = await Task.WhenAny(botTask, discordTask).ConfigureAwait(false);
                    }
                    else
                    {
                        await botTask.ConfigureAwait(false);
                        completed = botTask;
                    }

                    if (hasDiscordToken && discordTask != null && completed == discordTask)
                    {
                        var discordFailure = await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);

                        if (cancel.IsCancellationRequested)
                            break;

                        bool isAuthOrConfigFailure = discordFailure is InvalidOperationException ||
                            (discordFailure is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.Unauthorized) ||
                            (discordFailure != null && (discordFailure.Message.Contains("401") || discordFailure.Message.Contains("Unauthorized") || discordFailure.Message.Contains("token")));

                        if (isAuthOrConfigFailure)
                        {
                            bot.Log("Discord authentication or configuration failed. Disabling Discord and continuing with other services.");
                            hasDiscordToken = false;
                            discordCancellation?.Cancel();
                            discordCancellation?.Dispose();
                            discordCancellation = null;
                            discordTask = null;
                            await botTask.ConfigureAwait(false);
                        }
                        else
                        {
                            botCancellation.Cancel();
                            await ObserveTaskAsync(botTask, bot, "Bot").ConfigureAwait(false);

                            if (discordFailure == null)
                                bot.Log("Discord has terminated unexpectedly.");
                            else
                                bot.Log("Discord failed; restarting the Discord and bot sessions.");

                            (bot, sys, discordTask, discordCancellation) = await RestartDiscordAsync(
                                config, bot, sys, discordCancellation!, cancel).ConfigureAwait(false);
                            continue;
                        }
                    }

                    var botFailure = await ObserveTaskAsync(botTask, bot, "Bot").ConfigureAwait(false);
                    if (discordCancellation != null && discordTask != null)
                    {
                        discordCancellation.Cancel();
                        await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                        await sys.Disconnect().ConfigureAwait(false);
                    }

                    if (botFailure != null)
                    {
                        bot.Log("Bot has terminated due to an error; automatic reconnect is disabled.");
                        break;
                    }

                    bot.Log("Bot has terminated.");
                    if (cancel.IsCancellationRequested)
                        break;

                    bot.Log("Please wait... Attempting to reconnect in 10 seconds.");
                    await Task.Delay(10_000, cancel).ConfigureAwait(false);

                    if (hasDiscordToken && discordCancellation != null)
                    {
                        (bot, sys, discordTask, discordCancellation) = await RestartDiscordAsync(
                            config, bot, sys, discordCancellation, cancel).ConfigureAwait(false);
                    }
                    else
                    {
                        bot.Log("Bot is attempting a restart...");
                        bot = new CrossBot(config);
                        Globals.Bot = bot;
                        sys = new SysCord(bot);
                        Globals.Self = sys;
                    }
                }
            }
            finally
            {
                if (discordCancellation != null && discordTask != null)
                {
                    discordCancellation.Cancel();
                    await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                    await sys.Disconnect().ConfigureAwait(false);
                    discordCancellation.Dispose();
                }
            }
        }

        private static Task StartDiscord(SysCord sys, string token, CancellationToken cancel) =>
            Task.Run(() => sys.MainAsync(token, cancel), CancellationToken.None);

        private static async Task<Exception?> ObserveTaskAsync(Task task, CrossBot bot, string name)
        {
            try
            {
                await task.ConfigureAwait(false);
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                LogDiscordFailure(bot, ex, name);
                return ex;
            }
        }

        private static void LogDiscordFailure(CrossBot bot, Exception ex, string name = "Discord")
        {
            bot.Log($"{name} failed: {ex.Message}");
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                bot.Log(ex.StackTrace);
        }

        private static async Task<(CrossBot Bot, SysCord Sys, Task DiscordTask, CancellationTokenSource DiscordCancellation)> RestartDiscordAsync(
            CrossBotConfig config,
            CrossBot bot,
            SysCord sys,
            CancellationTokenSource discordCancellation,
            CancellationToken cancel)
        {
            bot.Log("Bot is attempting a restart...");
            bot = new CrossBot(config);
            Globals.Bot = bot;

            await sys.Disconnect().ConfigureAwait(false);
            discordCancellation.Dispose();

            sys = new SysCord(bot);
            Globals.Self = sys;
            discordCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            bot.Log("Restarting Discord.");
            var discordTask = StartDiscord(sys, config.Token, discordCancellation.Token);
            return (bot, sys, discordTask, discordCancellation);
        }
    }
}
