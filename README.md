# SysBot.ACNHOrders
Designed as a fully automated queue-based order bot that injects item orders directly onto your island's map and lets the player that queued pick them up, then leave. All dodo fetching, gate opening & closing, movement and dialoguing (including Tom Nook's or Isabelle's morning announcement progression) is automated.

Alternatively, you may use this as a fully automated connection, logging, advanced item dropping and map refresh bot (among other things) that restores the online session after a network or online crash/disconnect. [Read here](https://github.com/berichan/SysBot.ACNHOrders/wiki/Automatic-dodo-online-session-restoring) for setup instructions.

![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)

## See also

[SysBot.AnimalCrossing](https://github.com/kwsch/SysBot.AnimalCrossing)

[kwsch](https://github.com/kwsch) for the original project and everything that makes this work.

[Red](https://github.com/hp3721) for the original Dodo-fetch code, pointer logic and a bunch of other things that make this possible.

Resources:

[Read the wiki](https://github.com/berichan/SysBot.ACNHOrders/wiki) for setup instructions and faq.

[Watch the bot showcase](https://youtu.be/Y-_Tg8bwveY) to see it in action from the host's POV.

[Watch how to order items using ACNHMS](https://youtu.be/SWVAf7uyyuw) to be used by the end-user of your bot.

## Support Discord:

[<img src="https://canary.discordapp.com/api/guilds/771477382409879602/widget.png?style=banner2">](https://discord.gg/5bT8XK8dYe)

[sys-botbase](https://github.com/olliz0r/sys-botbase) client for remote control automation of Nintendo Switch consoles.

Uses [Discord.Net](https://github.com/discord-net/Discord.Net) as a dependency via NuGet.

## Discord command mode

New configurations use prefixed text commands by default. To use slash commands without the privileged Message Content or Server Members intents, set:

```json
{
  "CommandMode": "InteractionCommands",
  "SlashCommandSuffix": "my-island"
}
```

`SlashCommandSuffix` is optional in interaction mode and is normalized to lowercase (spaces become underscores). The bot registers its complete command set as guild commands, so each island should use its own Discord application and bot token; Discord limits an application to 100 guild chat-input commands, and this bot currently exposes 69. A suffix is useful for identifying an island's commands (for example `/order_my-island`) but does not make multiple full command sets fit in one application. Changing a suffix reconciles the guild command set on the next startup; rerun `/setup-embed` after changing it so persistent buttons use the current command namespace. If the value cannot fit Discord's 32-character command-name limit, startup fails with a configuration error. File orders use the `file` option on the corresponding `/order-nhi` command (such as `/order-nhi_my-island`) and do not require reading message attachments.

Text-command mode requires the Message Content and Server Members privileged intents to be enabled for the bot in Discord's Developer Portal. Interaction mode uses only non-privileged gateway intents; users can still use a pasted command by mentioning the bot, and the bot will point them to `/help`.

## Other Dependencies
Animal Crossing API logic is provided by [NHSE](https://github.com/kwsch/NHSE/).

# License
Refer to the `License.md` for details regarding licensing.
