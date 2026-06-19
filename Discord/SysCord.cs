using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NHSE.Core;
using SysBot.Base;
using static Discord.GatewayIntents;

namespace SysBot.ACNHOrders
{
    public sealed class SysCord
    {
        private readonly DiscordSocketClient _client;
        private readonly CrossBot Bot;
        public ulong Owner = ulong.MaxValue;
        public static bool ForwardersReady = false;

        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;
        private string? _islandCommandSuffix;

        public SysCord(CrossBot bot)
        {
            Bot = bot;

            var intents = Guilds | GuildMessages | DirectMessages | GuildMembers;
            if (!bot.Config.UseInteractionCommands)
                intents |= MessageContent;

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                GatewayIntents = intents,
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = Discord.Commands.RunMode.Sync,
                CaseSensitiveCommands = false,
            });

            _interactions = new InteractionService(_client, new InteractionServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = Discord.Interactions.RunMode.Sync,
            });

            _client.Log += Log;
            _commands.Log += Log;
            _interactions.Log += Log;

            _services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var map = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            return map.BuildServiceProvider();
        }

        private static Task Log(LogMessage msg)
        {
            Console.ForegroundColor = msg.Severity switch
            {
                LogSeverity.Critical => ConsoleColor.Red,
                LogSeverity.Error => ConsoleColor.Red,

                LogSeverity.Warning => ConsoleColor.Yellow,
                LogSeverity.Info => ConsoleColor.White,

                LogSeverity.Verbose => ConsoleColor.DarkGray,
                LogSeverity.Debug => ConsoleColor.DarkGray,
                _ => Console.ForegroundColor
            };

            var text = $"[{msg.Severity,8}] {msg.Source}: {msg.Message} {msg.Exception}";
            Console.WriteLine($"{DateTime.Now,-19} {text}");
            Console.ResetColor();

            LogUtil.LogText($"SysCord: {text}");

            return Task.CompletedTask;
        }

        public async Task MainAsync(string apiToken, CancellationToken token)
        {
            await InitCommands().ConfigureAwait(false);

            await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
            _client.Ready += ClientReady;

            await Task.Delay(5_000, token).ConfigureAwait(false);

            var game = Bot.Config.Name;
            if (!string.IsNullOrWhiteSpace(game))
                await _client.SetGameAsync(game).ConfigureAwait(false);

            var app = await _client.GetApplicationInfoAsync().ConfigureAwait(false);
            Owner = app.Owner.Id;

            foreach (var s in _client.Guilds)
                if (NewAntiAbuse.Instance.IsGlobalBanned(0, 0, s.OwnerId.ToString()) || NewAntiAbuse.Instance.IsGlobalBanned(0, 0, Owner.ToString()))
                    Environment.Exit(404);

            await MonitorStatusAsync(token).ConfigureAwait(false);
        }

        private async Task ClientReady()
        {
            if (ForwardersReady)
                return;
            ForwardersReady = true;

            await Task.Delay(1_000).ConfigureAwait(false);

            foreach (var cid in Bot.Config.LoggingChannels)
            {
                var c = (ISocketMessageChannel)_client.GetChannel(cid);
                if (c == null)
                {
                    Console.WriteLine($"{cid} is null or couldn't be found.");
                    continue;
                }
                static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";
                void Logger(string msg, string identity) => c.SendMessageAsync(GetMessage(msg, identity));
                Action<string, string> l = Logger;
                LogUtil.Forwarders.Add(l);
            }

            if (Bot.Config.UseInteractionCommands)
            {
                if (Bot.Config.UseIslandNameInSlashCommands)
                    await RegisterSuffixedCommandsAsync().ConfigureAwait(false);
                else
                    foreach (var g in _client.Guilds)
                        await _interactions.RegisterCommandsToGuildAsync(g.Id).ConfigureAwait(false);
            }

            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task RegisterSuffixedCommandsAsync()
        {
            string islandFile = $"{Bot.Config.IP}_IslandData.txt";
            string? islandName = null;

            for (int i = 0; i < 60; i++)
            {
                if (File.Exists(islandFile))
                {
                    islandName = File.ReadAllText(islandFile).Trim();
                    break;
                }
                await Task.Delay(500).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(islandName))
            {
                await Log(new LogMessage(LogSeverity.Warning, "SysCord",
                    "Island name file not found. Registering commands without island suffix.")).ConfigureAwait(false);
                foreach (var g in _client.Guilds)
                    await _interactions.RegisterCommandsToGuildAsync(g.Id).ConfigureAwait(false);
                return;
            }

            // Sanitize: lowercase, keep only [a-z0-9_-], replace spaces with _
            string sanitized = string.Concat(islandName.ToLowerInvariant().Select(c =>
                char.IsLetterOrDigit(c) ? c :
                c == ' ' ? '_' :
                c == '-' || c == '_' ? c :
                '_'));

            sanitized = sanitized.Trim('_');

            if (string.IsNullOrEmpty(sanitized))
            {
                await Log(new LogMessage(LogSeverity.Warning, "SysCord",
                    "Sanitized island name is empty. Registering commands without island suffix.")).ConfigureAwait(false);
                foreach (var g in _client.Guilds)
                    await _interactions.RegisterCommandsToGuildAsync(g.Id).ConfigureAwait(false);
                return;
            }

            // Ensure all command names fit within Discord's 32-char limit
            int maxCmdNameLen = 0;
            foreach (var module in _interactions.Modules)
                foreach (var cmd in module.SlashCommands)
                    if (cmd.Name.Length > maxCmdNameLen)
                        maxCmdNameLen = cmd.Name.Length;

            int maxSuffixLen = 32 - 1 - maxCmdNameLen;
            if (maxSuffixLen < 1)
            {
                await Log(new LogMessage(LogSeverity.Warning, "SysCord",
                    "Command names too long for island suffix. Registering without suffix.")).ConfigureAwait(false);
                foreach (var g in _client.Guilds)
                    await _interactions.RegisterCommandsToGuildAsync(g.Id).ConfigureAwait(false);
                return;
            }

            if (sanitized.Length > maxSuffixLen)
                sanitized = sanitized.Substring(0, maxSuffixLen);

            _islandCommandSuffix = $"_{sanitized}";

            var allCommands = new System.Collections.Generic.List<SlashCommandProperties>();

            foreach (var module in _interactions.Modules)
            {
                foreach (var cmd in module.SlashCommands)
                {
                    var builder = new SlashCommandBuilder()
                        .WithName($"{cmd.Name}{_islandCommandSuffix}")
                        .WithDescription(cmd.Description);

                    foreach (var param in cmd.Parameters)
                    {
                        var discordType = param.DiscordOptionType ?? ApplicationCommandOptionType.String;

                        var optBuilder = new SlashCommandOptionBuilder()
                            .WithName(param.Name)
                            .WithDescription(param.Description)
                            .WithType(discordType)
                            .WithRequired(param.IsRequired);

                        if (param.ChannelTypes != null)
                            foreach (var ct in param.ChannelTypes)
                                optBuilder.AddChannelType(ct);

                        if (param.MinValue.HasValue)
                            optBuilder.WithMinValue(param.MinValue.Value);

                        if (param.MaxValue.HasValue)
                            optBuilder.WithMaxValue(param.MaxValue.Value);

                        if (param.MinLength.HasValue)
                            optBuilder.WithMinLength(param.MinLength.Value);

                        if (param.MaxLength.HasValue)
                            optBuilder.WithMaxLength(param.MaxLength.Value);

                        if (param.Choices != null)
                            foreach (var choice in param.Choices)
                            {
                                if (choice.Value is int intVal)
                                    optBuilder.AddChoice(choice.Name, intVal);
                                else if (choice.Value is string strVal)
                                    optBuilder.AddChoice(choice.Name, strVal);
                                else if (choice.Value is double dblVal)
                                    optBuilder.AddChoice(choice.Name, dblVal);
                                else if (choice.Value is long lngVal)
                                    optBuilder.AddChoice(choice.Name, lngVal);
                                else if (choice.Value is float fltVal)
                                    optBuilder.AddChoice(choice.Name, fltVal);
                            }

                        if (param.IsAutocomplete)
                            optBuilder.WithAutocomplete(true);

                        builder.AddOption(optBuilder);
                    }

                    allCommands.Add(builder.Build());
                }
            }

            foreach (var g in _client.Guilds)
                await g.BulkOverwriteApplicationCommandAsync(allCommands.ToArray()).ConfigureAwait(false);

            await Log(new LogMessage(LogSeverity.Info, "SysCord",
                $"Registered {allCommands.Count} commands with island suffix '{_islandCommandSuffix}'.")).ConfigureAwait(false);
        }

        public async Task InitCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Always load old command modules and subscribe message handler.
            // In new mode, HandleMessageAsync only responds to bot mention prefix,
            // providing paste-compatibility without MessageContent intent.
            await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
            _client.MessageReceived += HandleMessageAsync;

            // Always subscribe interaction handler so embed buttons show a helpful
            // message in old mode instead of failing silently.
            _client.InteractionCreated += HandleInteractionAsync;

            if (Bot.Config.UseInteractionCommands)
                await _interactions.AddModulesAsync(assembly, _services).ConfigureAwait(false);
        }

        public async Task Disconnect()
        {
            if (_client == null)
                return;
            await _client.StopAsync().ConfigureAwait(false);
        }

        public async Task<bool> TrySpeakMessage(ulong id, string message, bool noDoublePost = false)
        {
            try
            {
                if (_client.ConnectionState != ConnectionState.Connected)
                    return false;
                var channel = _client.GetChannel(id);
                if (noDoublePost && channel is IMessageChannel msgChannel)
                {
                    var lastMsg = await msgChannel.GetMessagesAsync(1).FlattenAsync();
                    if (lastMsg != null && lastMsg.Any())
                        if (lastMsg.ElementAt(0).Content == message)
                            return true;
                }

                if (channel is IMessageChannel textChannel)
                    await textChannel.SendMessageAsync(message).ConfigureAwait(false);
                return true;
            }
            catch(Exception e)
            {
                if (e.StackTrace != null)
                    LogUtil.LogError($"SpeakMessage failed with:\n{e.Message}\n{e.StackTrace}", nameof(SysCord));
                else
                    LogUtil.LogError($"SpeakMessage failed with:\n{e.Message}", nameof(SysCord));
            }

            return false;
        }

        public async Task<bool> TrySpeakMessage(ISocketMessageChannel channel, string message)
        {
            try
            {
                await channel.SendMessageAsync(message).ConfigureAwait(false);
                return true;
            }
            catch { }

            return false;
        }

        private async Task HandleMessageAsync(SocketMessage arg)
        {
            if (arg is not SocketUserMessage msg)
                return;

            if (msg.Author.Id == _client.CurrentUser.Id || (!Bot.Config.IgnoreAllPermissions && msg.Author.IsBot))
                return;

            if (Bot.Config.UseInteractionCommands)
            {
                // New mode: check for pending NHI file uploads first
                if (await TryHandleNhiFileUpload(msg).ConfigureAwait(false))
                    return;

                // Then check for bot mention (pasted command compatibility)
                int pos = 0;
                if (msg.HasMentionPrefix(_client.CurrentUser, ref pos))
                {
                    // Skip any whitespace between mention and command text
                    while (pos < msg.Content.Length && char.IsWhiteSpace(msg.Content[pos]))
                        pos++;
                    bool handled = await TryHandleCommandAsync(msg, pos).ConfigureAwait(false);
                    if (handled)
                        return;
                }
                // Silently ignore all other messages in new mode
                return;
            }

            // Old mode: respond to text prefix as before
            int pos2 = 0;
            if (msg.HasStringPrefix(Bot.Config.Prefix, ref pos2))
            {
                bool handled = await TryHandleCommandAsync(msg, pos2).ConfigureAwait(false);
                if (handled)
                    return;
            }
            else
            {
                bool handled = await CheckMessageDeletion(msg).ConfigureAwait(false);
                if (handled)
                    return;
            }

            await TryHandleMessageAsync(msg).ConfigureAwait(false);
        }

        private async Task<bool> CheckMessageDeletion(SocketUserMessage msg)
        {
            var context = new SocketCommandContext(_client, msg);

            var usrId = msg.Author.Id;
            if (!Globals.Bot.Config.DeleteNonCommands || context.IsPrivate || msg.Author.IsBot || Globals.Bot.Config.CanUseSudo(usrId) || msg.Author.Id == Owner)
                return false;
            if (Globals.Bot.Config.Channels.Count < 1 || !Globals.Bot.Config.Channels.Contains(context.Channel.Id))
                return false;

            var msgText = msg.Content;
            var mention = msg.Author.Mention;

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Unknown Guild";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"Possible spam detected in {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Content: {msg}")).ConfigureAwait(false);

            await msg.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            await msg.Channel.SendMessageAsync($"{mention} - The order channels are for bot commands only.\nDeleted Message:```\n{msgText}\n```").ConfigureAwait(false);

            return true;
        }

        private async Task<bool> TryHandleNhiFileUpload(SocketUserMessage msg)
        {
            if (!EmbedInteractionModule.PendingNhiUploads.TryGetValue(msg.Author.Id, out var pending))
                return false;

            // Check if entry has expired
            if ((DateTime.Now - pending.Timestamp).TotalSeconds > 120)
            {
                EmbedInteractionModule.PendingNhiUploads.TryRemove(msg.Author.Id, out _);
                await msg.Channel.SendMessageAsync($"{msg.Author.Mention} - Your file upload request has expired. Please click the **File Order** button again.").ConfigureAwait(false);
                return true;
            }

            // Check if the upload is in the same channel
            if (msg.Channel.Id != pending.ChannelId)
                return false;

            // Look for an NHI attachment
            var nhiAttachment = msg.Attachments.FirstOrDefault(a =>
                a.Filename.EndsWith(".nhi", StringComparison.OrdinalIgnoreCase));

            if (nhiAttachment == null)
                return false;

            // Process the NHI file
            EmbedInteractionModule.PendingNhiUploads.TryRemove(msg.Author.Id, out _);

            var att = await NetUtil.DownloadNHIAsync(nhiAttachment).ConfigureAwait(false);
            if (!att.Success || att.Data == null)
            {
                await msg.Channel.SendMessageAsync($"{msg.Author.Mention} - Invalid NHI attachment. Please try again.").ConfigureAwait(false);
                return true;
            }

            var items = att.Data;

            string path = Path.Combine(OrderInteractionModule.LastOrderDirectory, $"{msg.Author.Id}");
            var itemArray = new ItemArrayEditor<Item>(items);
            File.WriteAllBytes(path, itemArray.Write());

            await QueueHelper.AttemptToQueueRequestAsync(items, msg.Author, msg.Channel, null, true,
                Globals.Bot.Config.OrderConfig.MaxQueueCount, async response =>
                {
                    await msg.Channel.SendMessageAsync($"{msg.Author.Mention} - {response}").ConfigureAwait(false);
                }).ConfigureAwait(false);

            return true;
        }

        private static async Task TryHandleMessageAsync(SocketMessage msg)
        {
            if (msg.Attachments.Count > 0)
            {
                await Task.CompletedTask.ConfigureAwait(false);
            }
        }

        private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, int pos)
        {
            var context = new SocketCommandContext(_client, msg);

            var mgr = Bot.Config;
            if (!Bot.Config.IgnoreAllPermissions)
            {
                if (!mgr.CanUseCommandUser(msg.Author.Id))
                {
                    await msg.Channel.SendMessageAsync("You are not permitted to use this command.").ConfigureAwait(false);
                    return true;
                }
                if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != Owner && !mgr.CanUseSudo(msg.Author.Id))
                {
                    await msg.Channel.SendMessageAsync("You can't use that command here.").ConfigureAwait(false);
                    return true;
                }
            }

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Unknown Guild";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"Executing command from {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Content: {msg}")).ConfigureAwait(false);
            var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

            if (result.Error == CommandError.UnknownCommand)
                return false;

            if (!result.IsSuccess)
                await msg.Channel.SendMessageAsync(result.ErrorReason).ConfigureAwait(false);
            return true;
        }

        private async Task HandleInteractionAsync(SocketInteraction arg)
        {
            if (!Bot.Config.UseInteractionCommands)
            {
                await arg.RespondAsync("This bot is running in text-command mode. Slash commands and interactive embeds are not available.", ephemeral: true);
                return;
            }

            var ctx = new SocketInteractionContext(_client, arg);

            var mgr = Bot.Config;
            if (!mgr.IgnoreAllPermissions)
            {
                if (!mgr.CanUseCommandUser(ctx.User.Id))
                {
                    await ctx.Interaction.RespondAsync("You are not permitted to use this command.", ephemeral: true);
                    return;
                }
                if (!mgr.CanUseCommandChannel(ctx.Channel.Id) && ctx.User.Id != Owner && !mgr.CanUseSudo(ctx.User.Id))
                {
                    await ctx.Interaction.RespondAsync("You can't use that command here.", ephemeral: true);
                    return;
                }
            }

            // Try suffixed command matching first (island name appended to command names)
            if (_islandCommandSuffix != null && arg is SocketSlashCommand slashCmd)
            {
                if (await TryExecuteSuffixedCommandAsync(ctx, slashCmd).ConfigureAwait(false))
                    return;
            }

            await _interactions.ExecuteCommandAsync(ctx, _services);
        }

        private async Task<bool> TryExecuteSuffixedCommandAsync(SocketInteractionContext ctx, SocketSlashCommand cmd)
        {
            string fullName = cmd.Data.Name;
            if (_islandCommandSuffix == null || !fullName.EndsWith(_islandCommandSuffix, StringComparison.OrdinalIgnoreCase))
                return false;

            string originalName = fullName.Substring(0, fullName.Length - _islandCommandSuffix.Length);

            var cmdInfo = _interactions.SlashCommands.FirstOrDefault(c =>
                c.Name.Equals(originalName, StringComparison.OrdinalIgnoreCase));

            if (cmdInfo == null)
                return false;

            await cmdInfo.ExecuteAsync(ctx, _services).ConfigureAwait(false);
            return true;
        }

        private async Task MonitorStatusAsync(CancellationToken token)
        {
            const int Interval = 20;
            UserStatus state = UserStatus.Idle;
            while (!token.IsCancellationRequested)
            {
                var time = DateTime.Now;
                var lastLogged = LogUtil.LastLogged;
                var delta = time - lastLogged;
                var gap = TimeSpan.FromSeconds(Interval) - delta;

                if (gap <= TimeSpan.Zero)
                {
                    var idle = !Bot.Config.AcceptingCommands ? UserStatus.DoNotDisturb : UserStatus.Idle;
                    if (idle != state)
                    {
                        state = idle;
                        await _client.SetStatusAsync(state).ConfigureAwait(false);
                    }

                    if (Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode && Bot.Config.DodoModeConfig.SetStatusAsDodoCode)
                        await _client.SetGameAsync($"Dodo code: {Bot.DodoCode}").ConfigureAwait(false);

                    await Task.Delay(2_000, token).ConfigureAwait(false);
                    continue;
                }

                var active = !Bot.Config.AcceptingCommands ? UserStatus.DoNotDisturb : UserStatus.Online;
                if (active != state)
                {
                    state = active;
                    await _client.SetStatusAsync(state).ConfigureAwait(false);
                }
                await Task.Delay(gap, token).ConfigureAwait(false);
            }
        }
    }
}
