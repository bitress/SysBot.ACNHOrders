using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ACNHMobileSpawner;
using Discord.Interactions;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class ControlInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("detach", "Detaches the virtual controller so the operator can use their own handheld controller temporarily.")]
        [RequireSudoInteraction]
        public async Task DetachAsync()
        {
            await RespondAsync("A controller detach request will be executed momentarily.");
            var bot = Globals.Bot;
            await bot.Connection.SendAsync(SwitchCommand.DetachController(), CancellationToken.None).ConfigureAwait(false);
        }

        [SlashCommand("toggle-requests", "Toggles whether public commands and orders are accepted.")]
        [RequireSudoInteraction]
        public async Task ToggleRequestsAsync()
        {
            bool value = (Globals.Bot.Config.AcceptingCommands ^= true);
            await RespondAsync($"Accepting public requests: {value}.");
        }

        [SlashCommand("toggle-mash-b", "Toggle whether or not the bot should mash the B button to ensure all dialogue is processed.")]
        [RequireSudoInteraction]
        public async Task ToggleMashB()
        {
            Globals.Bot.Config.DodoModeConfig.MashB = !Globals.Bot.Config.DodoModeConfig.MashB;
            await RespondAsync($"Mash B set to: {Globals.Bot.Config.DodoModeConfig.MashB}.");
        }

        [SlashCommand("toggle-refresh", "Toggle whether or not the bot should refresh the map.")]
        [RequireSudoInteraction]
        public async Task ToggleRefresh()
        {
            Globals.Bot.Config.DodoModeConfig.RefreshMap = !Globals.Bot.Config.DodoModeConfig.RefreshMap;
            await RespondAsync($"RefreshMap set to: {Globals.Bot.Config.DodoModeConfig.RefreshMap}.");
        }

        [SlashCommand("newdodo", "Tells the bot to restart the game and fetch a new dodo code.")]
        [RequireSudoInteraction]
        public async Task FetchNewDodo()
        {
            Globals.Bot.RestoreRestartRequested = true;
            await RespondAsync("Sending request to fetch a new dodo code.");
        }

        [SlashCommand("timer", "Tells the bot to restart the game after a delay and fetch a new dodo code.")]
        [RequireSudoInteraction]
        public async Task DelayFetchNewDodo([MinValue(0), MaxValue(1_440)] int minutes)
        {
            var channel = Context.Channel;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), CancellationToken.None).ConfigureAwait(false);
                Globals.Bot.RestoreRestartRequested = true;
                await Globals.Self.TrySpeakMessage(channel, "Fetching a new dodo code shortly.").ConfigureAwait(false);
            }, CancellationToken.None);
            await RespondAsync($"Sending request to fetch a new dodo code after {minutes} minutes.");
        }

        [SlashCommand("speak", "Tells the bot to speak during times when people are on the island.")]
        [RequireSudoInteraction]
        public async Task SpeakAsync(string message)
        {
            var saneString = message.Length > (int)OffsetHelper.ChatBufferSize ? message.Substring(0, (int)OffsetHelper.ChatBufferSize) : message;
            Globals.Bot.Speaks.Enqueue(new SpeakRequest(Context.User.Username, saneString));
            await RespondAsync($"I'll say `{saneString}` shortly.");
        }

        [SlashCommand("screen-on", "Turns the screen on.")]
        [RequireSudoInteraction]
        public async Task SetScreenOnAsync()
        {
            await SetScreen(true).ConfigureAwait(false);
        }

        [SlashCommand("screen-off", "Turns the screen off.")]
        [RequireSudoInteraction]
        public async Task SetScreenOffAsync()
        {
            await SetScreen(false).ConfigureAwait(false);
        }

        [SlashCommand("charge", "Prints the current battery percent of host console.")]
        [RequireSudoInteraction]
        public async Task GetChargeAsync()
        {
            await RespondAsync($"Last captured charge: {Globals.Bot.ChargePercent}%");
        }

        [SlashCommand("kill", "Kills the bot.")]
        [RequireSudoInteraction]
        public async Task KillBotAsync()
        {
            await RespondAsync($"Goodbye {Context.User.Mention}, remember me.");
            Environment.Exit(0);
        }

        [SlashCommand("ping", "Replies with pong if alive.")]
        public async Task PingAsync()
        {
            await RespondAsync($"Hi {Context.User.Mention}, Pong!");
        }

        [SlashCommand("makebat", "Creates a Restart.bat file in the bot folder.")]
        [RequireSudoInteraction]
        public async Task MakeBatAsync()
        {
            var botloc = AppDomain.CurrentDomain.BaseDirectory;
            var botapp = AppDomain.CurrentDomain.FriendlyName;
            var botname = "";
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
                botname = "Treasure Island";
            else
                botname = "Order Bot";

            var batinfo = $"TITLE {botname}\n@echo off\n:Start\ncd {botloc}\n{botapp}\n:: Wait 20 seconds before restarting.\nTIMEOUT / T 20\nGOTO: Start";
            using (StreamWriter writer = new StreamWriter("Restart.bat"))
            {
                writer.WriteLine($"{batinfo}");
            }
            await RespondAsync("I have made `Restart.bat`. Please check the bot folder for the file.");
        }

        private async Task SetScreen(bool on)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var bot = Globals.Bot;
            await bot.SetScreenCheck(on, CancellationToken.None, true).ConfigureAwait(false);
            await Context.Interaction.ModifyOriginalResponseAsync(properties =>
                properties.Content = "Screen state set to: " + (on ? "On" : "Off")).ConfigureAwait(false);
        }
    }
}
