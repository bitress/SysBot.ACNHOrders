using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using NHSE.Core;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    internal static class QueueHelper
    {
        internal static async Task AttemptToQueueRequestAsync(
            IReadOnlyCollection<Item> items,
            SocketUser orderer,
            ISocketMessageChannel msgChannel,
            VillagerRequest? vr,
            bool catalogue,
            int maxOrderCount,
            Func<string, Task> respondAsync)
        {
            if (!Globals.Bot.Config.AllowKnownAbusers && LegacyAntiAbuse.CurrentInstance.IsGlobalBanned(orderer.Id))
            {
                await respondAsync("You are not permitted to use this bot.");
                return;
            }

            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode || Globals.Bot.Config.SkipConsoleBotCreation)
            {
                await respondAsync("Orders are not currently accepted.");
                return;
            }

            if (GlobalBan.IsBanned(orderer.Id.ToString()))
            {
                await respondAsync("You have been banned for abuse. Order has not been accepted.");
                return;
            }

            var currentOrderCount = Globals.Hub.Orders.Count;
            if (currentOrderCount >= maxOrderCount)
            {
                var requestLimit = $"The queue limit has been reached, there are currently {currentOrderCount} players in the queue. Please try again later.";
                await respondAsync(requestLimit);
                return;
            }

            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, Globals.Bot.Config.DropConfig))
            {
                var unsafeItems = InternalItemTool.CurrentInstance.GetUnsafeItemNames(items);
                var unsafeList = string.Join(", ", unsafeItems);
                await respondAsync($"You are attempting to order items that will damage your save. Order not accepted.\r\nThe following item(s) are not safe: {unsafeList}");
                return;
            }

            if (items.Count > MultiItem.MaxOrder)
            {
                var clamped = $"Users are limited to {MultiItem.MaxOrder} items per command, You've asked for {items.Count}. All items above the limit have been removed.";
                await respondAsync(clamped);
                items = items.Take(40).ToArray();
            }

            var multiOrder = new MultiItem(items.ToArray(), catalogue, true, true);
            var requestInfo = new OrderRequest<Item>(multiOrder, multiOrder.ItemArray.Items.ToArray(), orderer.Id, QueueExtensions.GetNextID(), orderer, msgChannel, vr);

            IUserMessage? test = null;
            try
            {
                const string helper = "I've added you to the queue! I'll message you here when your order is ready";
                test = await orderer.SendMessageAsync(helper).ConfigureAwait(false);
            }
            catch (HttpException ex)
            {
                await respondAsync($"{ex.HttpCode}: {ex.Reason}! You must enable private messages in order to be queued!");
                return;
            }

            var result = QueueExtensions.AddToQueueSync(requestInfo, orderer.Mention, orderer.Username, out var msg);

            await respondAsync(msg);
            await orderer.SendMessageAsync(msg).ConfigureAwait(false);

            if (!result && test != null)
            {
                await test.DeleteAsync().ConfigureAwait(false);
            }
        }
    }
}
