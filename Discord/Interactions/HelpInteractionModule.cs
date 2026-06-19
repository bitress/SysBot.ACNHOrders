using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace SysBot.ACNHOrders
{
    public class HelpInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        public InteractionService? InteractService { get; set; }

        [SlashCommand("help", "Lists available commands or info about a specific command.")]
        [RequireSudoInteraction]
        public async Task HelpAsync([Discord.Interactions.Summary("command")] string? command = null)
        {
            var embed = new EmbedBuilder
            {
                Color = new Color(114, 137, 223),
                Description = command != null
                    ? $"Commands matching **{command}**:"
                    : "These are the commands you can use:"
            };

            foreach (var module in InteractService!.Modules)
            {
                string? description = null;
                HashSet<string> mentioned = new();

                foreach (var cmd in module.SlashCommands)
                {
                    if (command != null && !cmd.Name.Contains(command, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = cmd.Name;
                    if (mentioned.Contains(name))
                        continue;
                    if (cmd.Attributes.Any(z => z is RequireSudoInteractionAttribute) && !Globals.Bot.Config.CanUseSudo(Context.User.Id))
                        continue;

                    mentioned.Add(name);

                    var paramNames = string.Join(" ", cmd.Parameters.Select(p => $"<{p.Name}>"));
                    description += $"/{name} {paramNames}\n";
                }

                if (string.IsNullOrWhiteSpace(description))
                    continue;

                embed.AddField(x =>
                {
                    x.Name = module.Name;
                    x.Value = description;
                    x.IsInline = false;
                });
            }

            if (embed.Fields.Count == 0)
            {
                await RespondAsync($"Sorry, I couldn't find a command like **{command}**.", ephemeral: true);
                return;
            }

            await RespondAsync("Help has arrived!", embed: embed.Build(), ephemeral: true);
        }
    }
}
