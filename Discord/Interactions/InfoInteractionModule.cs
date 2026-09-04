using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace SysBot.ACNHOrders
{
    public class InfoInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        private const string detail = "I am an open source Discord bot powered by SysBot.NET, NHSE, ACNHMS and other open source software.";
        private const string repo = "https://github.com/berichan/SysBot.ACNHOrders";

        [SlashCommand("info", "Shows information about the bot.")]
        [RequireSudoInteraction]
        public async Task InfoAsync()
        {
            await DeferAsync(ephemeral: true).ConfigureAwait(false);
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = detail,
            };

            builder.AddField("Info",
                $"- [Source Code]({repo})\n" +
                $"- {Format.Bold("Owner")}: {app.Owner} ({app.Owner.Id})\n" +
                $"- {Format.Bold("Library")}: Discord.Net ({DiscordConfig.Version})\n" +
                $"- {Format.Bold("Uptime")}: {GetUptime()}\n" +
                $"- {Format.Bold("Runtime")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
                $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
                $"- {Format.Bold("Buildtime")}: {GetBuildTime()}\n"
            );

            builder.AddField("Stats",
                $"- {Format.Bold("Heap Size")}: {GetHeapSize()}MiB\n" +
                $"- {Format.Bold("Guilds")}: {Context.Client.Guilds.Count}\n" +
                $"- {Format.Bold("Channels")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
                $"- {Format.Bold("Cached Users")}: {Context.Client.Guilds.Sum(g => g.Users.Count)}\n"
            );

            await Context.Interaction.ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = "Here's a bit about me!";
                properties.Embed = builder.Build();
            }).ConfigureAwait(false);
        }

        private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

        private static string GetBuildTime()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null)
                return DateTime.Now.ToString(@"yy-MM-dd\.hh\:mm");
            return File.GetLastWriteTime(assembly.Location).ToString(@"yy-MM-dd\.hh\:mm");
        }
    }
}
