using System;
using System.Collections.Generic;
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
    public sealed class OrderModal : IModal
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

    public class OrderInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        public const string LastOrderDirectory = "UserOrder";
        public const string OrderMarker = "ORDER";
        public const string OrderCatMarker = "ORDERCAT";

        private static int MaxOrderCount => Globals.Bot.Config.OrderConfig.MaxQueueCount;
        private static Dictionary<ulong, DateTime> UserLastCommand = new();
        private static object commandSync = new();

        [SlashCommand("order", "Requests the bot add the item order to the queue.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestOrderAsync(
            [Summary("items")] string? items = null,
            [Summary("villager")] string? villager = null,
            [Summary("language")] string? language = null)
        {
            if (string.IsNullOrWhiteSpace(items))
            {
                var cb = new ComponentBuilder()
                    .WithButton("Open Order Form", Globals.Self.GetInteractionCustomId("open-order-modal"), ButtonStyle.Primary);
                await RespondAsync("Click to place your order:", components: cb.Build(), ephemeral: true);
                return;
            }

            await ProcessOrderInline(items, villager, language, false).ConfigureAwait(false);
        }

        [ComponentInteraction("open-order-modal")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task OpenOrderModal() => ShowOrderModal();

        [ComponentInteraction("open-order-modal:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task OpenSuffixedOrderModal(string _) => ShowOrderModal();

        private async Task ShowOrderModal()
        {
            var modal = new ModalBuilder()
                .WithTitle("Place an Order")
                .WithCustomId(Globals.Self.GetInteractionCustomId("order-modal"))
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

        [ModalInteraction("order-modal")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleOrderModal(OrderModal modal) => ProcessOrderInline(modal.Items, modal.Villager, modal.Language, false);

        [ModalInteraction("order-modal:*")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public Task HandleSuffixedOrderModal(string _, OrderModal modal) => ProcessOrderInline(modal.Items, modal.Villager, modal.Language, false);

        private async Task ProcessOrderInline(string request, string? villagerParam, string? languageParam, bool catalogue)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"order received by {Context.User.Username} - {request}", nameof(OrderInteractionModule));

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
                ? new[] { new Item(Item.NONE) }
                : ItemParser.GetItemsFromUserInput(combinedRequest, cfg.DropConfig, ItemDestination.FieldItemDropped).ToArray();

            var result = await AttemptToQueueRequest(items, Context.User, Context.Channel, vr, catalogue).ConfigureAwait(false);
            if (result.Accepted)
            {
                string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
                var marker = catalogue ? OrderCatMarker : OrderMarker;
                File.WriteAllText(path, marker + combinedRequest);
            }
            await SetDeferredResponseAsync(result.Message).ConfigureAwait(false);
        }

        [SlashCommand("ordercat", "Orders a catalogue of items, does not duplicate any items.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestCatalogueOrderAsync(
            [Summary("items")] string items,
            [Summary("villager")] string? villager = null)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"ordercat received by {Context.User.Username} - {items}", nameof(OrderInteractionModule));

            if (!string.IsNullOrWhiteSpace(villager))
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await SetDeferredResponseAsync("Villager injection is currently disabled.").ConfigureAwait(false);
                    return;
                }

                var internalName = villager.Trim();
                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await SetDeferredResponseAsync($"{villager} is not a valid internal villager name.").ConfigureAwait(false);
                    return;
                }

                var replace = VillagerResources.GetVillager(internalName);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(internalName));
            }

            var parsedItems = string.IsNullOrWhiteSpace(items) ? new Item[1] { new Item(Item.NONE) } : ItemParser.GetItemsFromUserInput(items, cfg.DropConfig, ItemDestination.FieldItemDropped);

            var result = await AttemptToQueueRequest(parsedItems, Context.User, Context.Channel, vr, true).ConfigureAwait(false);
            if (result.Accepted)
            {
                string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
                File.WriteAllText(path, OrderCatMarker + items);
            }
            await SetDeferredResponseAsync(result.Message).ConfigureAwait(false);
        }

        [SlashCommand("order-nhi", "Requests the bot an order of items in the NHI format.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestNHIOrderAsync(IAttachment file)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var att = await NetUtil.DownloadNHIAsync(file).ConfigureAwait(false);
            if (!att.Success || !(att.Data is Item[] items))
            {
                await SetDeferredResponseAsync("The attachment is not a valid NHI file.").ConfigureAwait(false);
                return;
            }

            var itemArray = new ItemArrayEditor<Item>(att.Data);
            var result = await AttemptToQueueRequest(items, Context.User, Context.Channel, null, true).ConfigureAwait(false);
            if (result.Accepted)
            {
                string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
                File.WriteAllBytes(path, itemArray.Write());
            }
            await SetDeferredResponseAsync(result.Message).ConfigureAwait(false);
        }

        [SlashCommand("lastorder", "Provides the user with their last order data.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestLastOrderAsync()
        {
            string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
            if (!File.Exists(path))
            {
                await RespondAsync($"<@{Context.User.Id}>, We do not have your last order logged, place an order and then you can use this command.", ephemeral: true);
                return;
            }

            string request = File.ReadAllText(path);

            if (request.StartsWith(OrderMarker))
            {
                var stringWithoutMarker = request[OrderMarker.Length..];
                var command = Globals.Self.GetSlashCommandName("order");
                await RespondAsync($"{Context.User.Mention}, your last order command was:\n`/{command} items:{stringWithoutMarker}`", ephemeral: true);
            }
            else if (request.StartsWith(OrderCatMarker))
            {
                var stringWithoutMarker = request[OrderCatMarker.Length..];
                var command = Globals.Self.GetSlashCommandName("ordercat");
                await RespondAsync($"{Context.User.Mention}, your last catalogue order command was:\n`/{command} items:{stringWithoutMarker}`", ephemeral: true);
            }
            else
            {
                var bytes = File.ReadAllBytes(path);
                var tempFileName = $"{Context.User.Id}.nhi";
                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
                File.WriteAllBytes(tempFilePath, bytes);

                try
                {
                    await RespondWithFileAsync(
                        tempFilePath,
                        tempFileName,
                        $"{Context.User.Mention}, here's your last ordered NHI file!",
                        ephemeral: true).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception e)
                    {
                        LogUtil.LogError($"Failed to delete temp NHI file {tempFilePath}: {e.Message}", nameof(OrderInteractionModule));
                    }
                }
            }
        }

        [SlashCommand("checkitems", "Check the item ids to find item id's that will not let order happen.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CheckItemAsync(string items)
        {
            var BadItemsList = "";
            var CheckItemN = "";

            var Bitems = FileUtil.GetEmbeddedResource("SysBot.ACNHOrders.Resources", "InternalHexList.txt");
            string[] CheckItems = items.Split(' ');

            foreach (var CheckItem in CheckItems)
                if (Bitems.Contains(CheckItem))
                {
                    ushort itemID = ItemParser.GetID(CheckItem);
                    if (itemID != Item.NONE)
                    {
                        var name = GameInfo.Strings.GetItemName(itemID);
                        CheckItemN = name + ": " + CheckItem;
                    }
                    BadItemsList = BadItemsList + CheckItemN + "\n";
                }

            if (BadItemsList == "")
                await RespondAsync("All items are safe to order.", ephemeral: true);
            else
                await RespondAsync($"The following items are not safe to order:\n`{BadItemsList}`", ephemeral: true);
        }

        [SlashCommand("preset", "Requests the bot an order of a preset created by the bot host.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestPresetOrderAsync(string name, string? villager = null)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            if (!string.IsNullOrWhiteSpace(villager))
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await SetDeferredResponseAsync("Villager injection is currently disabled.").ConfigureAwait(false);
                    return;
                }

                var internalName = villager.Trim();
                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await SetDeferredResponseAsync($"{villager} is not a valid internal villager name.").ConfigureAwait(false);
                    return;
                }

                var replace = VillagerResources.GetVillager(internalName);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(internalName));
            }

            var presetName = name.Trim();
            var preset = PresetLoader.GetPreset(cfg.OrderConfig, presetName);
            if (preset == null)
            {
                await SetDeferredResponseAsync($"{presetName} is not a valid preset.").ConfigureAwait(false);
                return;
            }

            var result = await AttemptToQueueRequest(preset, Context.User, Context.Channel, vr, true).ConfigureAwait(false);
            await SetDeferredResponseAsync(result.Message).ConfigureAwait(false);
        }

        [SlashCommand("listpresets", "Lists all the presets.")]
        public async Task RequestListPresetsAsync()
        {
            var bot = Globals.Bot;
            Directory.CreateDirectory(bot.Config.OrderConfig.NHIPresetsDirectory);
            DirectoryInfo dir = new DirectoryInfo(bot.Config.OrderConfig.NHIPresetsDirectory);
            FileInfo[] files = dir.GetFiles("*.nhi");
            string listnhi = "";
            foreach (FileInfo file in files)
                listnhi = listnhi + "\n " + Path.GetFileNameWithoutExtension(file.Name);

            await RespondAsync($"**Presets available are the following:** {listnhi}.");
        }

        [SlashCommand("uploadpreset", "Uploads file to add to preset folder.")]
        [RequireSudoInteraction]
        public async Task RequestUploadPresetAsync(IAttachment file)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var cfg = Globals.Bot.Config;
            var fileName = Path.GetFileName(file.Filename);
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(Path.GetExtension(fileName), ".nhi", StringComparison.OrdinalIgnoreCase))
            {
                await SetDeferredResponseAsync("Only .nhi preset files can be uploaded.").ConfigureAwait(false);
                return;
            }

            var url = file.Url;
            Directory.CreateDirectory(cfg.OrderConfig.NHIPresetsDirectory);
            var dest = Path.Combine(cfg.OrderConfig.NHIPresetsDirectory, fileName);
            await NetUtil.DownloadFileAsync(url, dest).ConfigureAwait(false);
            await SetDeferredResponseAsync("Received attachment!\n\nThe following file has been added to presets folder: " + fileName).ConfigureAwait(false);
        }

        [SlashCommand("queue", "View your position in the queue.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task ViewQueuePositionAsync()
        {
            var cooldown = Globals.Bot.Config.OrderConfig.PositionCommandCooldown;
            if (!CanCommand(Context.User.Id, cooldown, true))
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

        [SlashCommand("remove", "Remove yourself from the queue.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RemoveFromQueueAsync()
        {
            QueueExtensions.GetPosition(Context.User.Id, out var order);
            if (order == null)
            {
                await RespondAsync("Sorry, you are not in the queue, or your order is happening now.", ephemeral: true);
                return;
            }

            Globals.Hub.Orders.RemoveByUserId(Context.User.Id);
            await RespondAsync("Your order has been removed. You can rejoin the queue at any time.", ephemeral: true);
        }

        [SlashCommand("removeuser", "Remove someone from the queue.")]
        [RequireSudoInteraction]
        public async Task RemoveOtherFromQueueAsync(string id)
        {
            if (ulong.TryParse(id, out var res))
            {
                QueueExtensions.GetPosition(res, out var order);
                if (order == null)
                {
                    await RespondAsync($"{id} is not a valid ulong in the queue.", ephemeral: true);
                    return;
                }

                Globals.Hub.Orders.RemoveByUserId(res);
                await RespondAsync($"{id} ({order.VillagerName}) has been removed from the queue.");
            }
            else
                await RespondAsync($"{id} is not a valid u64.", ephemeral: true);
        }

        [SlashCommand("removealt", "Removes an identity (name-id) from the local user-to-villager AntiAbuse database.")]
        [RequireSudoInteraction]
        public async Task RemoveAltAsync(string identity)
        {
            if (NewAntiAbuse.Instance.Remove(identity))
                await RespondAsync($"{identity} has been removed from the database.");
            else
                await RespondAsync($"{identity} is not a valid identity.", ephemeral: true);
        }

        [SlashCommand("removealt-legacy", "Uses legacy database to remove an identity (name-id).")]
        [RequireSudoInteraction]
        public async Task RemoveLegacyAltAsync(string identity)
        {
            if (LegacyAntiAbuse.CurrentInstance.Remove(identity))
                await RespondAsync($"{identity} has been removed from the database.");
            else
                await RespondAsync($"{identity} is not a valid identity.", ephemeral: true);
        }

        [SlashCommand("visitors", "Print the list of visitors on the island (dodo restore mode only).")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task ShowVisitorList()
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode && Globals.Self.Owner != Context.User.Id)
            {
                await RespondAsync("You may only view visitors in dodo restore mode. Please respect the privacy of other orderers.", ephemeral: true);
                return;
            }

            await RespondAsync(Globals.Bot.VisitorList.VisitorFormattedString);
        }

        [SlashCommand("checkstate", "Prints whether or not the bot will restart the game for the next order.")]
        [RequireSudoInteraction]
        public async Task ShowDirtyStateAsync()
        {
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("There is no order state in dodo restore mode.");
                return;
            }

            await RespondAsync($"State: {(Globals.Bot.GameIsDirty ? "Bad" : "Good")}");
        }

        [SlashCommand("queuelist", "DMs the user the current list of names in the queue.")]
        [RequireSudoInteraction]
        public async Task ShowQueueListAsync()
        {
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("There is no queue in dodo restore mode.");
                return;
            }

            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            try
            {
                await Context.User.SendMessageAsync($"The following users are in the queue for {Globals.Bot.TownName}: \r\n{QueueExtensions.GetQueueString()}").ConfigureAwait(false);
                await SetDeferredResponseAsync("Sent you the queue list via DM.").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await SetDeferredResponseAsync($"{e.Message}: Are your DMs open?").ConfigureAwait(false);
            }
        }

        [SlashCommand("gametime", "Prints the last checked (current) in-game time.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetGameTime()
        {
            var bot = Globals.Bot;
            var cooldown = bot.Config.OrderConfig.PositionCommandCooldown;
            if (!CanCommand(Context.User.Id, cooldown, true))
            {
                await RespondAsync($"This command has a {cooldown} second cooldown. Use this bot responsibly.", ephemeral: true);
                return;
            }

            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                var nooksMessage = (bot.LastTimeState.Hour >= 22 || bot.LastTimeState.Hour < 8) ? "Nook's Cranny is closed" : "Nook's Cranny is expected to be open.";
                await RespondAsync($"The current in-game time is: {bot.LastTimeState} \r\n{nooksMessage}");
                return;
            }

            await RespondAsync($"Last order started at: {bot.LastTimeState}");
        }

        private async Task<QueueAttemptResult> AttemptToQueueRequest(IReadOnlyCollection<Item> items, SocketUser orderer, ISocketMessageChannel msgChannel, VillagerRequest? vr, bool catalogue = false)
        {
            if (!Context.Interaction.HasResponded)
                await DeferAsync(ephemeral: true).ConfigureAwait(false);

            return await QueueHelper.AttemptToQueueRequestDetailedAsync(
                items, orderer, msgChannel, vr, catalogue, MaxOrderCount).ConfigureAwait(false);
        }

        private Task SetDeferredResponseAsync(string message) =>
            Context.Interaction.ModifyOriginalResponseAsync(properties => properties.Content = message);

        public static bool CanCommand(ulong id, int secondsCooldown, bool addIfNotAdded)
        {
            if (secondsCooldown < 0)
                return true;
            lock (commandSync)
            {
                if (UserLastCommand.ContainsKey(id))
                {
                    bool inCooldownPeriod = Math.Abs((DateTime.Now - UserLastCommand[id]).TotalSeconds) < secondsCooldown;
                    if (addIfNotAdded && !inCooldownPeriod)
                    {
                        UserLastCommand.Remove(id);
                        UserLastCommand.Add(id, DateTime.Now);
                    }
                    return !inCooldownPeriod;
                }
                else if (addIfNotAdded)
                {
                    UserLastCommand.Add(id, DateTime.Now);
                }
                return true;
            }
        }
    }
}
