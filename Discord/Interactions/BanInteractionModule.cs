using System.Threading.Tasks;
using Discord.Interactions;

namespace SysBot.ACNHOrders
{
    public class BanInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ban", "Bans a user by their long number id.")]
        [RequireSudoInteraction]
        public async Task BanAsync(string id)
        {
            if (GlobalBan.IsBanned(id))
            {
                await RespondAsync($"{id} is already abuse-banned", ephemeral: true);
            }
            else
            {
                GlobalBan.Ban(id);
                await RespondAsync($"{id} has been abuse-banned.", ephemeral: true);
            }
        }

        [SlashCommand("unban", "Unbans a user by their long number id.")]
        [RequireSudoInteraction]
        public async Task UnBanAsync(string id)
        {
            if (GlobalBan.IsBanned(id))
            {
                GlobalBan.UnBan(id);
                await RespondAsync($"{id} has been abuse-unbanned.", ephemeral: true);
            }
            else
            {
                await RespondAsync($"{id} could not be found in the ban list.", ephemeral: true);
            }
        }

        [SlashCommand("checkban", "Checks a user's ban state by their long number id.")]
        [RequireSudoInteraction]
        public async Task CheckBanAsync(string id)
        {
            var msg = GlobalBan.IsBanned(id) ? $"{id} is abuse-banned" : $"{id} is not abuse-banned";
            await RespondAsync(msg, ephemeral: true);
        }

        [SlashCommand("restrict", "Temporarily restricts a user by their long number account id.")]
        [RequireSudoInteraction]
        public async Task RestrictAsync(ulong id)
        {
            if (GlobalBan.IsTempRestricted(id))
            {
                await RespondAsync($"{id} is already temporarily restricted", ephemeral: true);
            }
            else
            {
                GlobalBan.TempRestrict(id);
                await RespondAsync($"{id} has been temporarily restricted.", ephemeral: true);
            }
        }

        [SlashCommand("unrestrict", "Removes temporary restriction from a user by their long number account id.")]
        [RequireSudoInteraction]
        public async Task UnRestrictAsync(ulong id)
        {
            if (GlobalBan.IsTempRestricted(id))
            {
                GlobalBan.RemoveTempRestrict(id);
                await RespondAsync($"{id} has been removed from temporary restriction.", ephemeral: true);
            }
            else
            {
                await RespondAsync($"{id} is not temporarily restricted", ephemeral: true);
            }
        }

        [SlashCommand("checkrestrict", "Checks a user's temporary restriction state by their long number account id.")]
        [RequireSudoInteraction]
        public async Task CheckRestrictAsync(ulong id)
        {
            var msg = GlobalBan.IsTempRestricted(id) ? $"{id} is temporarily restricted" : $"{id} is not temporarily restricted";
            await RespondAsync(msg, ephemeral: true);
        }
    }
}
