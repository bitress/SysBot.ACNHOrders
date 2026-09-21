using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace SysBot.ACNHOrders
{
    public sealed class RequireSudoInteractionAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo command, IServiceProvider services)
        {
            var mgr = Globals.Bot.Config;
            if (mgr.CanUseSudo(context.User.Id) || context.User.Id == Globals.Self.Owner || mgr.IgnoreAllPermissions)
                return Task.FromResult(PreconditionResult.FromSuccess());

            if (context.User is not SocketGuildUser)
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));

            return Task.FromResult(PreconditionResult.FromError("You are not permitted to run this command."));
        }
    }
}
