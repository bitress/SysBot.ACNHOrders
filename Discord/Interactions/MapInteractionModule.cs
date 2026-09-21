using System.IO;
using System.Threading.Tasks;
using Discord.Interactions;

namespace SysBot.ACNHOrders
{
    public class MapInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("loadlayer", "Changes the current refresher layer to a new .nhl field item layer.")]
        [RequireSudoInteraction]
        public async Task SetFieldLayerAsync(string filename)
        {
            var bot = Globals.Bot;

            if (!bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("This command can only be used in dodo restore mode with refresh map set to true.", ephemeral: true);
                return;
            }

            var bytes = bot.ExternalMap.GetNHL(filename);

            if (bytes == null)
            {
                await RespondAsync($"File {filename} does not exist or does not have the correct .nhl extension.", ephemeral: true);
                return;
            }

            var req = new MapOverrideRequest(Context.User.Username, bytes, filename);
            bot.MapOverrides.Enqueue(req);

            await RespondAsync($"Map refresh layer set to: {Path.GetFileNameWithoutExtension(filename)}.");
            Globals.Bot.CLayer = $"{Path.GetFileNameWithoutExtension(filename)}";
        }
    }
}
