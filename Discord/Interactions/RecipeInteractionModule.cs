using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using NHSE.Core;

namespace SysBot.ACNHOrders
{
    public class RecipeInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("recipe", "Gets a list of DIY recipe IDs that contain the requested item name.")]
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

        [SlashCommand("recipe-lang", "Gets a list of DIY recipe IDs that contain the requested item name in a specific language.")]
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

        private async Task PrintItemsAsync(string itemName, IReadOnlyList<ComboItem> strings)
        {
            const int minLength = 2;
            if (itemName.Length <= minLength)
            {
                await RespondAsync($"Please enter a search term longer than {minLength} characters.", ephemeral: true);
                return;
            }

            foreach (var item in strings)
            {
                if (!string.Equals(item.Text, itemName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                {
                    await RespondAsync("Requested item is not a DIY recipe.", ephemeral: true);
                    return;
                }

                var msg = $"{item.Value:X4} {item.Text}: Recipe order code: {recipeID:X3}000016A2";
                await RespondAsync(Format.Code(msg), ephemeral: true);
                return;
            }

            var items = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var matches = new List<string>();
            foreach (var item in items)
            {
                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                    continue;

                var msg = $"{item.Value:X4} {item.Text}: Recipe order code: {recipeID:X3}000016A2";
                matches.Add(msg);
            }

            var result = string.Join(Environment.NewLine, matches);
            if (result.Length == 0)
            {
                await RespondAsync("No matches found.", ephemeral: true);
                return;
            }

            const int maxLength = 500;
            if (result.Length > maxLength)
                result = result.Substring(0, maxLength) + "...[truncated]";

            await RespondAsync(Format.Code(result), ephemeral: true);
        }
    }
}
