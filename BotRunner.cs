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
        public static async Task RunFrom(CrossBotConfig config, CancellationToken cancel, TwitchConfig? tConfig = null)
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

            bot.Log("Starting Discord.");
            var discordCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            var discordTask = StartDiscord(sys, config.Token, discordCancellation.Token);


            if (tConfig != null && !string.IsNullOrWhiteSpace(tConfig.Token))
            {
                bot.Log("Starting Twitch.");
                var _ = new TwitchCrossBot(tConfig, bot);
            }

            if (!string.IsNullOrWhiteSpace(config.SignalrConfig.URIEndpoint))
            {
                bot.Log("Starting Web.");
                var _ = new SignalrCrossBot(config.SignalrConfig, bot);
            }

            if (config.SkipConsoleBotCreation)
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
                    discordCancellation.Cancel();
                    await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                    await sys.Disconnect().ConfigureAwait(false);
                    discordCancellation.Dispose();
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
                    var completed = await Task.WhenAny(botTask, discordTask).ConfigureAwait(false);

                    if (completed == discordTask)
                    {
                        var discordFailure = await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                        botCancellation.Cancel();
                        await ObserveTaskAsync(botTask, bot, "Bot").ConfigureAwait(false);

                        if (cancel.IsCancellationRequested)
                            break;

                        if (discordFailure is InvalidOperationException)
                        {
                            bot.Log("Discord configuration is invalid; automatic restart is disabled.");
                            break;
                        }

                        if (discordFailure == null)
                            bot.Log("Discord has terminated unexpectedly.");
                        else
                            bot.Log("Discord failed; restarting the Discord and bot sessions.");

                        (bot, sys, discordTask, discordCancellation) = await RestartDiscordAsync(
                            config, bot, sys, discordCancellation, cancel).ConfigureAwait(false);
                        continue;
                    }

                    var botFailure = await ObserveTaskAsync(botTask, bot, "Bot").ConfigureAwait(false);
                    discordCancellation.Cancel();
                    await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                    await sys.Disconnect().ConfigureAwait(false);

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
                    (bot, sys, discordTask, discordCancellation) = await RestartDiscordAsync(
                        config, bot, sys, discordCancellation, cancel).ConfigureAwait(false);
                }
            }
            finally
            {
                discordCancellation.Cancel();
                await ObserveTaskAsync(discordTask, bot, "Discord").ConfigureAwait(false);
                await sys.Disconnect().ConfigureAwait(false);
                discordCancellation.Dispose();
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
