using System.Threading;
using System.Threading.Tasks;
using Discord.Interactions;

namespace SysBot.ACNHOrders
{
    public class AnchorInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("setanchor", "Sets one of the anchors required for the queue loop.")]
        [RequireSudoInteraction]
        public async Task SetAnchorAsync(int anchorId)
        {
            var bot = Globals.Bot;
            await Task.Delay(2_000, CancellationToken.None).ConfigureAwait(false);
            var success = await bot.UpdateAnchor(anchorId, CancellationToken.None).ConfigureAwait(false);
            var msg = success ? $"Successfully updated anchor {anchorId}." : $"Unable to update anchor {anchorId}.";
            await RespondAsync(msg);
        }

        [SlashCommand("loadanchor", "Loads one of the anchors required for the queue loop.")]
        [RequireSudoInteraction]
        public async Task SendAnchorBytesAsync(int anchorId)
        {
            var bot = Globals.Bot;
            await Task.Delay(2_000, CancellationToken.None).ConfigureAwait(false);
            var success = await bot.SendAnchorBytes(anchorId, CancellationToken.None).ConfigureAwait(false);
            var msg = success ? $"Successfully set player to anchor {anchorId}." : $"Unable to set player to anchor {anchorId}.";
            await RespondAsync(msg);
        }
    }
}
