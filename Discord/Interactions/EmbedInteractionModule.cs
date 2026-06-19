using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using NHSE.Core;
using NHSE.Villagers;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class EmbedInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        public static readonly ConcurrentDictionary<ulong, (DateTime Timestamp, ulong ChannelId)> PendingNhiUploads = new();

        [SlashCommand("setup-embed", "Creates the order embed with buttons in the specified channel.")]
        [RequireSudoInteraction]
        public async Task SetupEmbedAsync(ITextChannel? channel = null)
        {
            var target = channel ?? (Context.Channel as ITextChannel);
            if (target == null)
            {
                await RespondAsync("This command must be used in a text channel, or provide a channel.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Order Bot")
                .WithDescription("Click a button below to place an order or check your queue position.")
                .WithColor(Color.Blue);

            var cb = new ComponentBuilder()
                .WithButton("Place Order", "embed-normal-order", ButtonStyle.Primary)
                .WithButton("Catalogue Order", "embed-catalogue-order", ButtonStyle.Success)
                .WithButton("File Order", "embed-file-order", ButtonStyle.Secondary, row: 0)
                .WithButton("Queue Position", "embed-queue-position", ButtonStyle.Secondary, row: 1);

            await target.SendMessageAsync(embed: embed.Build(), components: cb.Build());
            await RespondAsync($"Embed created in {target.Mention}.", ephemeral: true);
        }

        [ComponentInteraction("embed-normal-order")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task OpenNormalOrderModal()
        {
            var modal = new ModalBuilder()
                .WithTitle("Place an Order")
                .WithCustomId("embed-order-modal")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Items")
                    .WithCustomId("modal-items")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithPlaceholder("Hex IDs (space-separated) or item names (comma-separated)")
                    .WithRequired(true)
                    .WithMaxLength(1000))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Villager (optional)")
                    .WithCustomId("modal-villager")
                    .WithStyle(TextInputStyle.Short)
                    .WithRequired(false)
                    .WithMaxLength(100))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Language (optional)")
                    .WithCustomId("modal-language")
                    .WithStyle(TextInputStyle.Short)
                    .WithPlaceholder("e.g., chs, de, fr")
                    .WithRequired(false)
                    .WithMaxLength(10));

            await Context.Interaction.RespondWithModalAsync(modal.Build());
        }

        [ComponentInteraction("embed-catalogue-order")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task OpenCatalogueOrderModal()
        {
            var modal = new ModalBuilder()
                .WithTitle("Catalogue Order")
                .WithCustomId("embed-catalogue-modal")
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Items")
                    .WithCustomId("modal-items")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithPlaceholder("Hex IDs (space-separated) or item names (comma-separated)")
                    .WithRequired(true)
                    .WithMaxLength(1000))
                .AddTextInput(new TextInputBuilder()
                    .WithLabel("Villager (optional)")
                    .WithCustomId("modal-villager")
                    .WithStyle(TextInputStyle.Short)
                    .WithRequired(false)
                    .WithMaxLength(100));

            await Context.Interaction.RespondWithModalAsync(modal.Build());
        }

        [ComponentInteraction("embed-file-order")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestFileUpload()
        {
            if (PendingNhiUploads.TryGetValue(Context.User.Id, out var existing))
            {
                if ((DateTime.Now - existing.Timestamp).TotalSeconds > 120)
                    PendingNhiUploads.TryRemove(Context.User.Id, out _);
                else
                {
                    await RespondAsync("You already have a pending file upload request. Please upload your .nhi file in the channel.", ephemeral: true);
                    return;
                }
            }

            PendingNhiUploads[Context.User.Id] = (DateTime.Now, Context.Channel.Id);
            await RespondAsync("Please upload your .nhi file in this channel within 120 seconds.", ephemeral: true);
        }

        [ComponentInteraction("embed-queue-position")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task ViewQueuePositionFromEmbed()
        {
            var cooldown = Globals.Bot.Config.OrderConfig.PositionCommandCooldown;
            if (!OrderInteractionModule.CanCommand(Context.User.Id, cooldown, true))
            {
                await RespondAsync($"This command has a {cooldown} second cooldown. Use this bot responsibly.", ephemeral: true);
                return;
            }

            var position = QueueExtensions.GetPosition(Context.User.Id, out _);
            if (position < 0)
            {
                await RespondAsync("Sorry, you are not in the queue, or your order is happening now.", ephemeral: true);
                return;
            }

            var message = $"{Context.User.Mention} - You are in the order queue. Position: {position}.";
            if (position > 1)
                message += $" Your predicted ETA is {QueueExtensions.GetETA(position)}.";
            else
                message += " Your order will start after the current order is complete!";

            await RespondAsync(message, ephemeral: true);
        }

        [ModalInteraction("embed-order-modal")]
        public async Task HandleEmbedOrderModal(SocketModal modal)
        {
            var items = modal.Data.Components.First(x => x.CustomId == "modal-items").Value;
            var villager = modal.Data.Components.FirstOrDefault(x => x.CustomId == "modal-villager")?.Value;
            var language = modal.Data.Components.FirstOrDefault(x => x.CustomId == "modal-language")?.Value;

            await ProcessModalOrderInline(items, villager, language, false).ConfigureAwait(false);
        }

        [ModalInteraction("embed-catalogue-modal")]
        public async Task HandleEmbedCatalogueModal(SocketModal modal)
        {
            var items = modal.Data.Components.First(x => x.CustomId == "modal-items").Value;
            var villager = modal.Data.Components.FirstOrDefault(x => x.CustomId == "modal-villager")?.Value;

            await ProcessModalOrderInline(items, villager, null, true).ConfigureAwait(false);
        }

        private async Task ProcessModalOrderInline(string request, string? villagerParam, string? languageParam, bool catalogue)
        {
            request = StripCommandPrefix(request);
            if (string.IsNullOrWhiteSpace(request))
            {
                await RespondAsync("No items provided. Please include at least one item.", ephemeral: true);
                return;
            }

            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"embed order received by {Context.User.Username} - {request}", nameof(EmbedInteractionModule));

            if (!string.IsNullOrWhiteSpace(villagerParam))
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await RespondAsync("Villager injection is currently disabled.", ephemeral: true);
                    return;
                }

                var internalName = villagerParam.Trim();
                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await RespondAsync($"{villagerParam} is not a valid internal villager name.", ephemeral: true);
                    return;
                }

                if (VillagerOrderParser.IsUnadoptable(internalName))
                {
                    await RespondAsync($"{villagerParam} is not adoptable. Order setup required for this villager is unnecessary.", ephemeral: true);
                    return;
                }

                var replace = VillagerResources.GetVillager(internalName);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(internalName));
            }

            var combinedRequest = request;
            if (!string.IsNullOrWhiteSpace(languageParam))
                combinedRequest = $"{languageParam}, {request}";

            var items = string.IsNullOrWhiteSpace(combinedRequest)
                ? new Item[1] { new Item(Item.NONE) }
                : ItemParser.GetItemsFromUserInput(combinedRequest, cfg.DropConfig, ItemDestination.FieldItemDropped).ToArray();

            string path = Path.Combine(OrderInteractionModule.LastOrderDirectory, $"{Context.User.Id}");
            var marker = catalogue ? OrderInteractionModule.OrderCatMarker : OrderInteractionModule.OrderMarker;
            File.WriteAllText(path, marker + combinedRequest);

            await QueueHelper.AttemptToQueueRequestAsync(items, Context.User, Context.Channel, vr, catalogue,
                Globals.Bot.Config.OrderConfig.MaxQueueCount, msg => RespondAsync(msg, ephemeral: true)).ConfigureAwait(false);
        }

        private static string StripCommandPrefix(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var trimmed = input.TrimStart();

            var prefix = Globals.Bot.Config.Prefix;
            if (!string.IsNullOrEmpty(prefix) && trimmed.StartsWith(prefix))
                trimmed = trimmed.Substring(prefix.Length).TrimStart();
            if (trimmed.StartsWith("!"))
                trimmed = trimmed.Substring(1).TrimStart();
            if (trimmed.StartsWith("$"))
                trimmed = trimmed.Substring(1).TrimStart();

            string[] knownCommands = { "order", "ordercat", "order-nhi", "drop", "dropdiy" };
            foreach (var cmd in knownCommands)
            {
                if (trimmed.StartsWith(cmd, StringComparison.InvariantCultureIgnoreCase)
                    && (trimmed.Length == cmd.Length || char.IsWhiteSpace(trimmed[cmd.Length])))
                {
                    trimmed = trimmed.Substring(cmd.Length).TrimStart();
                    break;
                }
            }

            return trimmed;
        }
    }
}
