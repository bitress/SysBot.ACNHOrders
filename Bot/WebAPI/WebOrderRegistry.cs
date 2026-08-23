using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NHSE.Core;

namespace SysBot.ACNHOrders.WebAPI
{
    /// <summary>
    /// Thread-safe registry tracking active and recent web/API orders.
    /// </summary>
    public static class WebOrderRegistry
    {
        private static readonly ConcurrentDictionary<string, WebOrderRequest<Item>> _ordersById = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<ulong, WebOrderRequest<Item>> _ordersByUser = new();

        /// <summary>
        /// Registers a new web order in the registry.
        /// </summary>
        public static void Register(WebOrderRequest<Item> order)
        {
            _ordersById[order.OrderIdString] = order;
            _ordersByUser[order.UserGuid] = order;
            CleanExpired();
        }

        /// <summary>
        /// Retrieves an order by its ID string or User Guid.
        /// </summary>
        public static bool TryGet(string idOrUser, out WebOrderRequest<Item>? order)
        {
            if (!string.IsNullOrWhiteSpace(idOrUser))
            {
                if (_ordersById.TryGetValue(idOrUser.Trim(), out order))
                    return true;

                if (ulong.TryParse(idOrUser.Trim(), out var ulongId))
                {
                    if (_ordersByUser.TryGetValue(ulongId, out order))
                        return true;
                }
            }

            order = null;
            return false;
        }

        public static bool TryGetByUserId(ulong userId, out WebOrderRequest<Item>? order)
        {
            return _ordersByUser.TryGetValue(userId, out order);
        }

        public static bool TryGetByOrderId(string orderId, out WebOrderRequest<Item>? order)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                order = null;
                return false;
            }
            return _ordersById.TryGetValue(orderId.Trim(), out order);
        }

        /// <summary>
        /// Cancels and removes an order from queue and registry.
        /// </summary>
        public static bool Cancel(string idOrUser, string reason = "Cancelled by user.")
        {
            if (TryGet(idOrUser, out var order) && order != null)
            {
                order.ForceCancel(reason);
                Globals.Hub.Orders.RemoveByUserId(order.UserGuid);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns all web orders.
        /// </summary>
        public static IEnumerable<WebOrderRequest<Item>> GetAll()
        {
            return _ordersById.Values;
        }

        /// <summary>
        /// Cleans up completed/cancelled orders older than 1 hour.
        /// </summary>
        public static void CleanExpired()
        {
            var cutoff = DateTime.UtcNow.AddHours(-1);
            foreach (var kvp in _ordersById)
            {
                if ((kvp.Value.Status == "completed" || kvp.Value.Status == "cancelled" || kvp.Value.Status == "error") && kvp.Value.UpdatedAt < cutoff)
                {
                    _ordersById.TryRemove(kvp.Key, out _);
                    _ordersByUser.TryRemove(kvp.Value.UserGuid, out _);
                }
            }
        }
    }
}
