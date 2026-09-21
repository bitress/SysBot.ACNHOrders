using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Interactions;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class RemoteControlInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static CrossBot Bot => Globals.Bot;

        [SlashCommand("click", "Clicks the specified button.")]
        [RequireSudoInteraction]
        public async Task ClickAsync(SwitchButton button)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var b = Globals.Bot;
            await b.Connection.SendAsync(SwitchCommand.Click(button, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync($"{b.Connection.Name} has performed: {button}").ConfigureAwait(false);
        }

        [SlashCommand("setstick", "Sets the stick to the specified position.")]
        [RequireSudoInteraction]
        public async Task SetStickAsync(SwitchStick stick, short x, short y, [MinValue(0), MaxValue(60_000)] int ms = 1_000)
        {
            if (!Enum.IsDefined(typeof(SwitchStick), stick))
            {
                await RespondAsync($"Unknown stick: {stick}", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var b = Bot;
            await b.Connection.SendAsync(SwitchCommand.SetStick(stick, x, y, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync($"{b.Connection.Name} has performed: {stick}").ConfigureAwait(false);
            await Task.Delay(ms).ConfigureAwait(false);
            await b.Connection.SendAsync(SwitchCommand.ResetStick(stick, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await FollowupAsync($"{b.Connection.Name} has reset the stick position.", ephemeral: true).ConfigureAwait(false);
        }

        [SlashCommand("readmemory", "Reads memory from the requested offset and writes it to the bot directory.")]
        [RequireSudoInteraction]
        public async Task ReadMemoryAsync(uint offset, [MinValue(1), MaxValue(1_048_576)] int length)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var b = Bot;
            var result = await b.Connection.ReadBytesAsync(offset, length, CancellationToken.None).ConfigureAwait(false);
            System.IO.File.WriteAllBytes("dump.bin", result);
            await CompleteDeferredAsync("Done.").ConfigureAwait(false);
        }

        [SlashCommand("writememory", "Writes memory to the requested offset.")]
        [RequireSudoInteraction]
        public async Task WriteMemoryAsync(uint offset, string hex)
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var b = Bot;
            var data = GetBytesFromHexString(hex.Replace(" ", ""));
            await b.Connection.WriteBytesAsync(data, offset, CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync("Done.").ConfigureAwait(false);
        }

        [SlashCommand("readcommand", "Writes the requested command to the sysmodule and awaits a return value.")]
        [RequireSudoInteraction]
        public async Task ReadCommandAsync([MinValue(1), MaxValue(1_048_576)] int size, string command)
        {
            var b = Bot;
            var data = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
            await RespondAsync($"Sending `{command}` and waiting for {size}-byte result.", ephemeral: true).ConfigureAwait(false);
            var ret = await b.SwitchConnectedConnection.ReadRaw(data, size, CancellationToken.None).ConfigureAwait(false);
            await FollowupAsync($"`{command}` returned with result: {System.Text.Encoding.UTF8.GetString(ret)}", ephemeral: true).ConfigureAwait(false);
        }

        [SlashCommand("unfreeze", "Unfreezes everything.")]
        [RequireSudoInteraction]
        public async Task UnfreezeAll()
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var data = System.Text.Encoding.ASCII.GetBytes($"freezeClear\r\n");
            await Bot.SwitchConnectedConnection.SendRaw(data, CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync("Unfrozen all previously frozen values").ConfigureAwait(false);
        }

        [SlashCommand("setfreezedelay", "Configures the freeze delay in milliseconds between 3 and 10000.")]
        [RequireSudoInteraction]
        public async Task SetFreezeDelay(int ms)
        {
            if (ms < 3 || ms > 10000)
            {
                await RespondAsync("Error! Freeze rate must be between 3 and 10000!", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var data = System.Text.Encoding.ASCII.GetBytes($"configure freezeRate {ms}\r\n");
            await Bot.SwitchConnectedConnection.SendRaw(data, CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync($"Set freeze rate to: {ms}").ConfigureAwait(false);
        }

        [SlashCommand("freeze-pause", "Pauses all freeze values until unpause is called.")]
        [RequireSudoInteraction]
        public async Task FreezePause()
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            await Bot.SwitchConnectedConnection.SetFreezePauseState(true, CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync("Freeze has been paused.").ConfigureAwait(false);
        }

        [SlashCommand("freeze-unpause", "Unpauses all freeze values.")]
        [RequireSudoInteraction]
        public async Task FreezeUnpause()
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            await Bot.SwitchConnectedConnection.SetFreezePauseState(false, CancellationToken.None).ConfigureAwait(false);
            await CompleteDeferredAsync("Freeze has been unpaused.").ConfigureAwait(false);
        }

        private Task CompleteDeferredAsync(string message) =>
            Context.Interaction.ModifyOriginalResponseAsync(properties => properties.Content = message);

        private static byte[] GetBytesFromHexString(string seed)
        {
            return Enumerable.Range(0, seed.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(seed.Substring(x, 2), 16))
                .Reverse().ToArray();
        }
    }
}
