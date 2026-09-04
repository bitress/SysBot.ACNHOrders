using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using NHSE.Core;
using NHSE.Villagers;

namespace SysBot.ACNHOrders
{
    public class VillagerInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("injectvillager", "Injects a villager based on the internal name.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerAsync(string name, int index = 0)
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("Villagers cannot be injected in order mode.", ephemeral: true);
                return;
            }

            if (!Globals.Bot.Config.AllowVillagerInjection)
            {
                await RespondAsync("Villager injection is currently disabled.", ephemeral: true);
                return;
            }

            var internalName = name;
            var nameSearched = internalName;

            if (!VillagerResources.IsVillagerDataKnown(internalName))
                internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

            if (internalName == default)
            {
                await RespondAsync($"{nameSearched} is not a valid internal villager name.", ephemeral: true);
                return;
            }

            if (index > byte.MaxValue || index < 0)
            {
                await RespondAsync($"{index} is not a valid index", ephemeral: true);
                return;
            }

            var replace = VillagerResources.GetVillager(internalName);
            var extraMsg = string.Empty;
            if (VillagerOrderParser.IsUnadoptable(internalName))
                extraMsg += " Please note that you will not be able to adopt this villager.";
            var responseChannel = Context.Channel;
            var userMention = Context.User.Mention;

            var request = new VillagerRequest(Context.User.Username, replace, (byte)index, GameInfo.Strings.GetVillager(internalName))
            {
                OnFinish = success =>
                {
                    var reply = success
                        ? $"{nameSearched} has been injected by the bot at Index {index}. Please go talk to them!{extraMsg}"
                        : "Failed to inject villager. Please tell the bot owner to look at the logs!";
                    _ = Globals.Self.TrySpeakMessage(responseChannel, $"{userMention}: {reply}");
                }
            };

            Globals.Bot.VillagerInjections.Enqueue(request);
            await RespondAsync("Villager inject request has been added to the queue and will be injected momentarily. I will reply to you once this has completed.");
        }

        [SlashCommand("multivillager", "Injects multiple villagers based on internal names.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task MultiVillagerAsync(string names)
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("Villagers cannot be injected in order mode.", ephemeral: true);
                return;
            }

            if (!Globals.Bot.Config.AllowVillagerInjection)
            {
                await RespondAsync("Villager injection is currently disabled.", ephemeral: true);
                return;
            }

            var villagerNames = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var bot = Globals.Bot;
            int index = 0;
            int count = villagerNames.Length;
            var responseChannel = Context.Channel;
            var userMention = Context.User.Mention;

            if (count < 1)
            {
                await RespondAsync("No villager names provided.", ephemeral: true);
                return;
            }

            foreach (var nameLookup in villagerNames)
            {
                var internalName = nameLookup.Trim();
                var nameSearched = internalName;

                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await RespondAsync($"{nameSearched} is not a valid internal villager name.", ephemeral: true);
                    return;
                }

                if (index > byte.MaxValue || index < 0)
                {
                    await RespondAsync($"{index} is not a valid index", ephemeral: true);
                    return;
                }

                var replace = VillagerResources.GetVillager(internalName);
                var extraMsg = string.Empty;
                if (VillagerOrderParser.IsUnadoptable(internalName))
                    extraMsg += " Please note that you will not be able to adopt this villager.";

                var slot = index;
                var request = new VillagerRequest(Context.User.Username, replace, (byte)index, GameInfo.Strings.GetVillager(internalName))
                {
                    OnFinish = success =>
                    {
                        var reply = success
                            ? $"{nameSearched} has been injected by the bot at Index {slot}. Please go talk to them!{extraMsg}"
                            : "Failed to inject villager. Please tell the bot owner to look at the logs!";
                        _ = Globals.Self.TrySpeakMessage(responseChannel, $"{userMention}: {reply}");
                    }
                };

                bot.VillagerInjections.Enqueue(request);
                index = (index + 1) % 10;
            }

            var addMsg = count > 1 ? $"Villager inject request for {count} villagers have" : "Villager inject request has";
            await RespondAsync($"{addMsg} been added to the queue and will be injected momentarily. I will reply to you once this has completed.");
        }

        [SlashCommand("villagers", "Prints the list of villagers currently on the island.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerListAsync()
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await RespondAsync("Villagers on the island may be replaceable by adding them to your order command.", ephemeral: true);
                return;
            }

            await RespondAsync($"The following villagers are on {Globals.Bot.TownName}: {Globals.Bot.Villagers.LastVillagers}.");
        }

        [SlashCommand("villagername", "Gets the internal name of a villager.")]
        [RequireQueueRoleInteraction(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerNameAsync(string name, string? language = null)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await RespondAsync("Lookup commands are not accepted.", ephemeral: true);
                return;
            }

            GameStrings strings;
            if (language != null)
                strings = GameInfo.GetStrings(language);
            else
                strings = GameInfo.Strings;

            var map = strings.VillagerMap;
            var result = map.FirstOrDefault(z => string.Equals(name, z.Value.Replace(" ", string.Empty), StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrWhiteSpace(result.Key))
            {
                await RespondAsync($"No villager found of name {name}.", ephemeral: true);
                return;
            }
            await RespondAsync($"{name}={result.Key}", ephemeral: true);
        }
    }
}
