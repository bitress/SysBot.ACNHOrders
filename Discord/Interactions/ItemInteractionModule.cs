using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using NHSE.Core;

namespace SysBot.ACNHOrders
{
    public class ItemInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("lookup", "Gets a list of items that contain the request string.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync(string name)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }
            var strings = GameInfo.Strings.ItemDataSource;
            await PrintItemsAsync(name, strings).ConfigureAwait(false);
        }

        [SlashCommand("lookup-lang", "Gets a list of items that contain the request string in a specific language.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsLangAsync(string language, string name)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }
            var strings = GameInfo.GetStrings(language).ItemDataSource;
            await PrintItemsAsync(name, strings).ConfigureAwait(false);
        }

        private async Task PrintItemsAsync(string itemName, System.Collections.Generic.IReadOnlyList<ComboItem> strings)
        {
            const int minLength = 2;
            if (itemName.Length <= minLength)
            {
                await RespondAsync($"Please enter a search term longer than {minLength} characters.", ephemeral: true);
                return;
            }

            var exact = ItemParser.GetItem(itemName, strings);
            if (!exact.IsNone)
            {
                var msg = $"{exact.ItemId:X4} {itemName}";
                if (msg == "02F8 vine")
                    msg = "3107 vine";
                if (msg == "02F7 glowing moss")
                    msg = "3106 glowing moss";
                await RespondAsync(Format.Code(msg), ephemeral: true);
                return;
            }

            var matches = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var result = string.Join(Environment.NewLine, matches.Select(z => $"{z.Value:X4} {z.Text}"));

            if (result.Length == 0)
            {
                await RespondAsync("No matches found.", ephemeral: true);
                return;
            }

            const int maxLength = 500;
            if (result.Length > maxLength)
            {
                var ordered = matches.OrderBy(z => LevenshteinDistance.Compute(z.Text, itemName));
                result = string.Join(Environment.NewLine, ordered.Select(z => $"{z.Value:X4} {z.Text}"));
                result = result.Substring(0, maxLength) + "...[truncated]";
            }

            await RespondAsync(Format.Code(result), ephemeral: true);
        }

        [SlashCommand("item", "Gets the info for an item.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetItemInfoAsync(string hex)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }

            ushort itemID = ItemParser.GetID(hex);
            if (itemID == Item.NONE)
            {
                await RespondAsync("Invalid item requested.", ephemeral: true);
                return;
            }

            var name = GameInfo.Strings.GetItemName(itemID);
            var result = ItemInfo.GetItemInfo(itemID);
            if (result.Length == 0)
                await RespondAsync($"No customization data available for the requested item ({name}).", ephemeral: true);
            else
                await RespondAsync($"{name}:\r\n{result}", ephemeral: true);
        }

        [SlashCommand("stack", "Stacks an item and prints the hex code.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task StackAsync(string hex, int count)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }

            ushort itemID = ItemParser.GetID(hex);
            if (itemID == Item.NONE || count < 1 || count > 99)
            {
                await RespondAsync("Invalid item requested.", ephemeral: true);
                return;
            }

            var ct = count - 1;
            var item = new Item(itemID) { Count = (ushort)ct };
            var msg = ItemParser.GetItemText(item);
            await RespondAsync(msg, ephemeral: true);
        }

        [SlashCommand("customize", "Customizes an item and prints the hex code.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CustomizeAsync(string hex, int cust1, int cust2)
            => await CustomizeImpl(hex, cust1 + cust2);

        private async Task CustomizeImpl(string hex, int sum)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }

            ushort itemID = ItemParser.GetID(hex);
            if (itemID == Item.NONE)
            {
                await RespondAsync("Invalid item requested.", ephemeral: true);
                return;
            }
            if (sum <= 0)
            {
                await RespondAsync("No customization data specified.", ephemeral: true);
                return;
            }

            var remake = ItemRemakeUtil.GetRemakeIndex(itemID);
            if (remake < 0)
            {
                await RespondAsync("No customization data available for the requested item.", ephemeral: true);
                return;
            }

            int body = sum & 7;
            int fabric = sum >> 5;
            if (fabric > 7 || ((fabric << 5) | body) != sum)
            {
                await RespondAsync("Invalid customization data specified.", ephemeral: true);
                return;
            }

            var info = ItemRemakeInfoData.List[remake];
            bool hasBody = body == 0 || body <= info.ReBodyPatternNum;
            bool hasFabric = fabric == 0 || info.GetFabricDescription(fabric) != "Invalid";

            if (!hasBody || !hasFabric)
            {
                await RespondAsync("Requested customization for item appears to be invalid.", ephemeral: true);
                return;
            }

            var item = new Item(itemID) { BodyType = body, PatternChoice = fabric };
            var msg = ItemParser.GetItemText(item);
            await RespondAsync(msg, ephemeral: true);
        }
    }
}
