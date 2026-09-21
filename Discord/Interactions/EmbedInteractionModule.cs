using System;
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
    public sealed class EmbedOrderModal : IModal
    {
        public string Title => "Place an Order";

        [ModalTextInput("modal-items", TextInputStyle.Paragraph)]
        public string Items { get; set; } = string.Empty;

        [ModalTextInput("modal-villager")]
        [RequiredInput(false)]
        public string? Villager { get; set; }

        [ModalTextInput("modal-language")]
        [RequiredInput(false)]
        public string? Language { get; set; }
    }

    public sealed class EmbedCatalogueModal : IModal
    {
        public string Title => "Catalogue Order";

        [ModalTextInput("modal-items", TextInputStyle.Paragraph)]
        public string Items { get; set; } = string.Empty;

        [ModalTextInput("modal-villager")]
        [RequiredInput(false)]
        public string? Villager { get; set; }
    }

    public class EmbedInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("setup-embed", "Creates the order embed with buttons in the specified channel.")]
        [RequireSudoInteraction]
        public async Task SetupEmbedAsync(ITextChannel? channel = null)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var target = channel ?? (Context.Channel as ITextChannel);
            if (target == null)
            {
                await SetDeferredResponseAsync("This command must be used in a text channel, or provide a channel.").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Order Bot")
                .WithDescription("Click a button below to place an order or check your queue position.")
                .WithColor(Color.Blue);

            var cb = new ComponentBuilder()
                .WithButton("Place Order", Globals.Self.GetInteractionCustomId("embed-normal-order"), ButtonStyle.Primary)
                .WithButton("Catalogue Order", Globals.Self.GetInteractionCustomId("embed-catalogue-order"), ButtonStyle.Success)
                .WithButton("File Order", Globals.Self.GetInteractionCustomId("embed-file-order"), ButtonStyle.Secondary, row: 0)
                .WithButton("Queue Position", Globals.Self.GetInteractionCustomId("embed-queue-position"), ButtonStyle.Secondary, row: 1);

            await target.SendMessageAsync(embed: embed.Build(), components: cb.Build()).ConfigureAwait(false);
            await SetDeferredResponseAsync($"Embed created in {target.Mention}.").ConfigureAwait(false);
        }

        [ComponentInteraction("embed-normal-order")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task OpenNormalOrderModal() => ShowNormalOrderModal();

        [ComponentInteraction("embed-normal-order:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task OpenSuffixedNormalOrderModal(string _) => ShowNormalOrderModal();

        private async Task ShowNormalOrderModal()
        {
            var modal = new ModalBuilder()
                .WithTitle("Place an Order")
                .WithCustomId(Globals.Self.GetInteractionCustomId("embed-order-modal"))
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
        public Task OpenCatalogueOrderModal() => ShowCatalogueOrderModal();

        [ComponentInteraction("embed-catalogue-order:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task OpenSuffixedCatalogueOrderModal(string _) => ShowCatalogueOrderModal();

        private async Task ShowCatalogueOrderModal()
        {
            var modal = new ModalBuilder()
                .WithTitle("Catalogue Order")
                .WithCustomId(Globals.Self.GetInteractionCustomId("embed-catalogue-modal"))
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
            var command = Globals.Self.GetSlashCommandName("order-nhi");
            await RespondAsync($"Use `/{command}` and attach your `.nhi` file to its **file** option.", ephemeral: true);
        }

        [ComponentInteraction("embed-file-order:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task RequestSuffixedFileUpload(string _) => RequestFileUpload();

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

        [ComponentInteraction("embed-queue-position:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task ViewSuffixedQueuePositionFromEmbed(string _) => ViewQueuePositionFromEmbed();

        [ModalInteraction("embed-order-modal")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleEmbedOrderModal(EmbedOrderModal modal) =>
            ProcessModalOrderInline(modal.Items, modal.Villager, modal.Language, false);

        [ModalInteraction("embed-order-modal:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleSuffixedEmbedOrderModal(string _, EmbedOrderModal modal) =>
            ProcessModalOrderInline(modal.Items, modal.Villager, modal.Language, false);

        [ModalInteraction("embed-catalogue-modal")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleEmbedCatalogueModal(EmbedCatalogueModal modal) =>
            ProcessModalOrderInline(modal.Items, modal.Villager, null, true);

        [ModalInteraction("embed-catalogue-modal:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleSuffixedEmbedCatalogueModal(string _, EmbedCatalogueModal modal) =>
            ProcessModalOrderInline(modal.Items, modal.Villager, null, true);

        private async Task ProcessModalOrderInline(string request, string? villagerParam, string? languageParam, bool catalogue)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            request = StripCommandPrefix(request);
            if (string.IsNullOrWhiteSpace(request))
            {
                await SetDeferredResponseAsync("No items provided. Please include at least one item.").ConfigureAwait(false);
                return;
            }

            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"embed order received by {Context.User.Username} - {request}", nameof(EmbedInteractionModule));

            if (!string.IsNullOrWhiteSpace(villagerParam))
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await SetDeferredResponseAsync("Villager injection is currently disabled.").ConfigureAwait(false);
                    return;
                }

                var internalName = villagerParam.Trim();
                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await SetDeferredResponseAsync($"{villagerParam} is not a valid internal villager name.").ConfigureAwait(false);
                    return;
                }

                if (VillagerOrderParser.IsUnadoptable(internalName))
                {
                    await SetDeferredResponseAsync($"{villagerParam} is not adoptable. Order setup required for this villager is unnecessary.").ConfigureAwait(false);
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

            var marker = catalogue ? OrderInteractionModule.OrderCatMarker : OrderInteractionModule.OrderMarker;
            var result = await QueueHelper.AttemptToQueueRequestDetailedAsync(
                items, Context.User, Context.Channel, vr, catalogue,
                Globals.Bot.Config.OrderConfig.MaxQueueCount).ConfigureAwait(false);
            if (result.Accepted)
            {
                string path = Path.Combine(OrderInteractionModule.LastOrderDirectory, $"{Context.User.Id}");
                File.WriteAllText(path, marker + combinedRequest);
            }
            await SetDeferredResponseAsync(result.Message).ConfigureAwait(false);
        }

        private Task SetDeferredResponseAsync(string message) =>
            Context.Interaction.ModifyOriginalResponseAsync(properties => properties.Content = message);

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
