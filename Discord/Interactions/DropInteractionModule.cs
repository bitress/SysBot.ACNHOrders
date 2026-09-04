using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.Net;
using NHSE.Core;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class DropInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static int MaxRequestCount => Globals.Bot.Config.DropConfig.MaxDropCount;

        [SlashCommand("clean", "Picks up items around the bot.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestCleanAsync()
        {
            if (!await GetDropAvailability().ConfigureAwait(false))
                return;

            if (!Globals.Bot.Config.AllowClean)
            {
                await RespondAsync("Clean functionality is currently disabled.", ephemeral: true);
                return;
            }
            Globals.Bot.CleanRequested = true;
            await RespondAsync("A clean request will be executed momentarily.");
        }

        [SlashCommand("code", "Prints the Dodo Code for the island.")]
        [RequireSudoInteraction]
        public async Task RequestDodoCodeAsync()
        {
            var draw = Globals.Bot.DodoImageDrawer;
            var txt = $"Dodo Code for {Globals.Bot.TownName}: {Globals.Bot.DodoCode}.";
            if (draw != null)
            {
                var path = draw.GetProcessedDodoImagePath();
                if (path != null)
                {
                    await RespondWithFileAsync(path, Path.GetFileName(path), txt, ephemeral: true).ConfigureAwait(false);
                    return;
                }
            }

            await RespondAsync(txt, ephemeral: true);
        }

        [SlashCommand("senddodo", "Sends the Dodo Code via DM. Only works in dodo restore mode.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestRestoreLoopDodoAsync()
        {
            var cfg = Globals.Bot.Config;
            Globals.Bot.DisUserID = ($"{Context.User.Id}");
            if (!Globals.Bot.Config.DodoModeConfig.AllowSendDodo && !Globals.Bot.Config.CanUseSudo(Context.User.Id) && Globals.Self.Owner != Context.User.Id)
            {
                await RespondAsync("Dodo code requests are currently disabled.", ephemeral: true);
                return;
            }
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("This command is only available in Dodo restore mode.", ephemeral: true);
                return;
            }

            string[] Checklist = File.ReadAllLines("banlist.txt", Encoding.UTF8);
            int indexS = Array.FindIndex(Checklist, row => row.Contains(Context.User.Id.ToString()));
            if (indexS != -1)
            {
                await RespondAsync("You are currently not allowed to use the bot. Dodo code will not be sent.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            try
            {
                if (cfg.FieldLayerName != "name")
                {
                    var MapFile = ($"{Globals.Bot.Config.FieldLayerNHLDirectory}/{Globals.Bot.CLayer}.png");
                    await Context.User.SendMessageAsync($"Dodo Code for {Globals.Bot.TownName}: {Globals.Bot.DodoCode}.\n{Globals.Bot.TownName} is currently set to the following layer: {Globals.Bot.CLayer}.").ConfigureAwait(false);
                    if (File.Exists($"{MapFile}"))
                    {
                        await Context.User.SendFileAsync($"{MapFile}");
                    }
                    var guildName = Context.Guild?.Name ?? "Direct Message";
                    await Globals.Self.TrySpeakMessage(Globals.Bot.Config.DodoModeConfig.SentDodoChannels, $"[{DateTime.Now:MM-dd hh:mm:ss tt}] The Dodo code was sent to <@{Context.User.Id}> - {Context.User.Id} from `{guildName}` server.").ConfigureAwait(false);
                }
                else
                {
                    await Context.User.SendMessageAsync($"Dodo Code for {Globals.Bot.TownName}: {Globals.Bot.DodoCode}.").ConfigureAwait(false);
                }
            }
            catch (HttpException ex)
            {
                await Context.Interaction.ModifyOriginalResponseAsync(properties =>
                    properties.Content = $"{ex.Message}: Private messages must be open to use this command. I won't leak the Dodo code in this channel!").ConfigureAwait(false);
                return;
            }

            var acknowledgement = "Sent you the Dodo code via DM.";
            var reaction = Globals.Bot.Config.DodoModeConfig.SuccessfulDodoCodeSendReaction;
            if (!string.IsNullOrWhiteSpace(reaction))
            {
                try
                {
                    IEmote emote = reaction.StartsWith("<") ? Emote.Parse(reaction) : new Emoji(reaction);
                    acknowledgement += $" {emote}";
                }
                catch
                {
                    LogUtil.LogError($"Could not parse {reaction} as an emote.", "Config");
                }
            }

            await Context.Interaction.ModifyOriginalResponseAsync(properties => properties.Content = acknowledgement).ConfigureAwait(false);
        }

        [SlashCommand("drop", "Drops a custom item (or items).")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestDropAsync(string items)
        {
            var cfg = Globals.Bot.Config;
            var parsed = ItemParser.GetItemsFromUserInput(items, cfg.DropConfig, cfg.DropConfig.UseLegacyDrop ? ItemDestination.PlayerDropped : ItemDestination.HeldItem);

            MultiItem.StackToMax(parsed);
            await DropItems(parsed).ConfigureAwait(false);
        }

        [SlashCommand("dropdiy", "Drops a DIY recipe with the requested recipe ID(s).")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestDropDIYAsync(string recipes)
        {
            var items = ItemParser.GetDIYsFromUserInput(recipes);
            await DropItems(items).ConfigureAwait(false);
        }

        [SlashCommand("setturnips", "Sets all the week's turnips (minus Sunday) to a certain value.")]
        [RequireSudoInteraction]
        public async Task RequestTurnipSetAsync(int value)
        {
            var bot = Globals.Bot;
            var responseChannel = Context.Channel;
            var userMention = Context.User.Mention;
            bot.StonkRequests.Enqueue(new TurnipRequest(Context.User.Username, value)
            {
                OnFinish = success =>
                {
                    var reply = success
                        ? $"All turnip values successfully set to {value}!"
                        : "Catastrophic failure.";
                    _ = Globals.Self.TrySpeakMessage(responseChannel, $"{userMention}: {reply}");
                }
            });
            await RespondAsync($"Queued all turnip values to be set to {value}.");
        }

        [SlashCommand("setturnipsmax", "Sets all the week's turnips (minus Sunday) to 999,999,999.")]
        [RequireSudoInteraction]
        public async Task RequestTurnipMaxSetAsync() => await RequestTurnipSetAsync(999999999);

        private async Task DropItems(System.Collections.Generic.IReadOnlyCollection<Item> items)
        {
            if (!await GetDropAvailability().ConfigureAwait(false))
                return;

            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, Globals.Bot.Config.DropConfig))
            {
                await RespondAsync("You are attempting to drop items that will damage your save. Drop request not accepted.", ephemeral: true);
                return;
            }

            string? notice = null;
            if (items.Count > MaxRequestCount)
            {
                notice = $"Users are limited to {MaxRequestCount} items per command; the excess items were removed.";
                items = items.Take(MaxRequestCount).ToArray();
            }

            var requestInfo = new ItemRequest(Context.User.Username, items);
            Globals.Bot.Injections.Enqueue(requestInfo);

            var msg = $"Item drop request{(requestInfo.Item.Count > 1 ? "s" : string.Empty)} will be executed momentarily.";
            if (notice != null)
                msg = $"{notice}\n{msg}";
            await RespondAsync(msg);
        }

        private async Task<bool> GetDropAvailability()
        {
            var cfg = Globals.Bot.Config;

            if (cfg.CanUseSudo(Context.User.Id) || Globals.Self.Owner == Context.User.Id)
                return true;

            if (Globals.Bot.CurrentUserId == Context.User.Id)
                return true;

            if (!cfg.AllowDrop)
            {
                await RespondAsync("AllowDrop is currently set to false.", ephemeral: true);
                return false;
            }
            else if (!cfg.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("You are only permitted to use this command while on the island during your order, and only if you have forgotten something in your order.", ephemeral: true);
                return false;
            }

            return true;
        }
    }
}
