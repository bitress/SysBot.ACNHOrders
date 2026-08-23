using System;
using NHSE.Core;

namespace SysBot.ACNHOrders.WebAPI
{
    /// <summary>
    /// Order notifier representing an order submitted via Web API or Socket API.
    /// Tracks real-time lifecycle status, predicted ETA, and Dodo codes.
    /// </summary>
    public class WebOrderRequest<T> : IACNHOrderNotifier<T> where T : Item, new()
    {
        public T[] Order { get; }
        public VillagerRequest? VillagerOrder { get; }
        public ulong UserGuid { get; }
        public ulong OrderID { get; }
        public string OrderIdString { get; }
        public string VillagerName { get; }
        public string Trader { get; }
        public Action<CrossBot>? OnFinish { private get; set; }

        public string Status { get; private set; } = "queued";
        public string StatusMessage { get; private set; } = "Order is in queue.";
        public string? DodoCode { get; private set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public WebOrderRequest(
            T[] order,
            ulong userGuid,
            ulong orderId,
            string orderIdString,
            string traderName,
            string villagerName,
            VillagerRequest? villagerOrder = null)
        {
            Order = order;
            UserGuid = userGuid;
            OrderID = orderId;
            OrderIdString = string.IsNullOrWhiteSpace(orderIdString) ? orderId.ToString() : orderIdString;
            Trader = traderName;
            VillagerName = string.IsNullOrWhiteSpace(villagerName) ? traderName : villagerName;
            VillagerOrder = villagerOrder;
        }

        public void OrderCancelled(CrossBot routine, string msg, bool faulted)
        {
            Status = faulted ? "error" : "cancelled";
            StatusMessage = msg;
            UpdatedAt = DateTime.UtcNow;
            OnFinish?.Invoke(routine);
        }

        public void OrderInitializing(CrossBot routine, string msg)
        {
            Status = "preparing";
            StatusMessage = string.IsNullOrWhiteSpace(msg) ? "Your order is starting. Preparing your island..." : msg;
            UpdatedAt = DateTime.UtcNow;
        }

        public void OrderReady(CrossBot routine, string msg, string dodo)
        {
            Status = "ready";
            DodoCode = dodo;
            StatusMessage = string.IsNullOrWhiteSpace(msg) ? $"Your order is ready! Dodo code is: {dodo}" : $"{msg}. Dodo code is: {dodo}";
            UpdatedAt = DateTime.UtcNow;
        }

        public void OrderFinished(CrossBot routine, string msg)
        {
            Status = "completed";
            StatusMessage = string.IsNullOrWhiteSpace(msg) ? "Your order has been completed. Thank you!" : msg;
            UpdatedAt = DateTime.UtcNow;
            OnFinish?.Invoke(routine);
        }

        public void SendNotification(CrossBot routine, string msg)
        {
            StatusMessage = msg;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ForceCancel(string reason)
        {
            Status = "cancelled";
            StatusMessage = reason;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
