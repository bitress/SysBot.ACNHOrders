using System;
using System.Collections.Generic;
using System.Text.Json;
using SocketAPI;

namespace SysBot.ACNHOrders.WebAPI
{
    /// <summary>
    /// Socket API Controller exposing endpoints for TCP Socket clients.
    /// Compatible with SocketAPIServer reflection discovery.
    /// </summary>
    [SocketAPIController]
    public class WebApiController
    {
        [SocketAPIEndpoint]
        public static object? GetStatus(string? args)
        {
            return WebApiService.GetStatus();
        }

        [SocketAPIEndpoint]
        public static object? GetDodo(string? args)
        {
            string? orderId = null;
            string? userId = null;

            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("order_id", out var oid))
                        orderId = oid.GetString();
                    else if (doc.RootElement.TryGetProperty("id", out var id))
                        orderId = id.GetString();

                    if (doc.RootElement.TryGetProperty("user_id", out var uid))
                        userId = uid.GetString();
                }
                catch
                {
                    orderId = args.Trim();
                }
            }

            return WebApiService.GetDodo(orderId, userId);
        }

        [SocketAPIEndpoint]
        public static object? GetQueue(string? args)
        {
            return WebApiService.GetQueue();
        }

        [SocketAPIEndpoint]
        public static object? GetOrderStatus(string? args)
        {
            string id = string.Empty;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                        id = idProp.GetString() ?? "";
                    else if (doc.RootElement.TryGetProperty("order_id", out var oidProp))
                        id = oidProp.GetString() ?? "";
                    else if (doc.RootElement.TryGetProperty("user_id", out var uidProp))
                        id = uidProp.GetString() ?? "";
                }
                catch
                {
                    id = args.Trim();
                }
            }

            return WebApiService.GetOrderStatus(id);
        }

        [SocketAPIEndpoint]
        public static object? RequestOrder(string? args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return new { success = false, message = "Arguments must not be empty." };

            string? orderText = null;
            string? villagerName = null;
            string? presetName = null;
            string? username = null;
            string? orderId = null;
            ulong? userId = null;
            List<string>? itemsList = null;

            try
            {
                using var doc = JsonDocument.Parse(args);
                var root = doc.RootElement;

                if (root.TryGetProperty("order", out var oProp))
                    orderText = oProp.GetString();
                else if (root.TryGetProperty("items", out var itProp))
                {
                    if (itProp.ValueKind == JsonValueKind.String)
                        orderText = itProp.GetString();
                    else if (itProp.ValueKind == JsonValueKind.Array)
                    {
                        itemsList = new List<string>();
                        foreach (var el in itProp.EnumerateArray())
                            itemsList.Add(el.GetString() ?? "");
                    }
                }
                else if (root.TryGetProperty("item", out var iProp))
                    orderText = iProp.GetString();

                if (root.TryGetProperty("villager", out var vProp))
                    villagerName = vProp.GetString();

                if (root.TryGetProperty("preset", out var pProp))
                    presetName = pProp.GetString();

                if (root.TryGetProperty("username", out var uProp))
                    username = uProp.GetString();

                if (root.TryGetProperty("order_id", out var oidProp))
                    orderId = oidProp.GetString();
                else if (root.TryGetProperty("id", out var idProp))
                    orderId = idProp.GetString();

                if (root.TryGetProperty("user_id", out var uidProp))
                {
                    if (uidProp.ValueKind == JsonValueKind.Number && uidProp.TryGetUInt64(out var u64))
                        userId = u64;
                    else if (uidProp.ValueKind == JsonValueKind.String && ulong.TryParse(uidProp.GetString(), out var u64p))
                        userId = u64p;
                }
            }
            catch (JsonException)
            {
                orderText = args.Trim();
            }

            return WebApiService.SubmitOrder(orderText, villagerName, presetName, username, orderId, userId, itemsList);
        }

        [SocketAPIEndpoint]
        public static object? CancelOrder(string? args)
        {
            string id = string.Empty;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                        id = idProp.GetString() ?? "";
                    else if (doc.RootElement.TryGetProperty("order_id", out var oidProp))
                        id = oidProp.GetString() ?? "";
                }
                catch
                {
                    id = args.Trim();
                }
            }

            return WebApiService.CancelOrder(id);
        }

        [SocketAPIEndpoint]
        public static object? RequestDrop(string? args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return new { success = false, message = "Arguments must not be empty." };

            string? itemsText = null;
            string? dropType = "items";
            string? username = "SocketClient";
            int? maxCount = null;
            List<string>? itemsList = null;

            try
            {
                using var doc = JsonDocument.Parse(args);
                var root = doc.RootElement;

                if (root.TryGetProperty("items", out var itProp))
                {
                    if (itProp.ValueKind == JsonValueKind.String)
                        itemsText = itProp.GetString();
                    else if (itProp.ValueKind == JsonValueKind.Array)
                    {
                        itemsList = new List<string>();
                        foreach (var el in itProp.EnumerateArray())
                            itemsList.Add(el.GetString() ?? "");
                    }
                }
                else if (root.TryGetProperty("item", out var iProp))
                    itemsText = iProp.GetString();
                else if (root.TryGetProperty("request", out var rProp))
                    itemsText = rProp.GetString();

                if (root.TryGetProperty("type", out var tProp))
                    dropType = tProp.GetString() ?? "items";

                if (root.TryGetProperty("username", out var uProp))
                    username = uProp.GetString();

                if (root.TryGetProperty("count", out var cntProp) && cntProp.TryGetInt32(out var cVal))
                    maxCount = cVal;
            }
            catch (JsonException)
            {
                itemsText = args.Trim();
            }

            return WebApiService.SubmitDrop(itemsText, dropType, username, maxCount, itemsList);
        }

        [SocketAPIEndpoint]
        public static object? RequestClean(string? args)
        {
            return WebApiService.SubmitClean();
        }

        [SocketAPIEndpoint]
        public static object? SetTurnips(string? args)
        {
            int price = 999999999;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("value", out var vProp) && vProp.TryGetInt32(out var vp))
                        price = vp;
                    else if (doc.RootElement.TryGetProperty("price", out var prProp) && prProp.TryGetInt32(out var prp))
                        price = prp;
                }
                catch
                {
                    if (int.TryParse(args.Trim(), out var p))
                        price = p;
                }
            }

            return WebApiService.SubmitTurnips(price);
        }

        [SocketAPIEndpoint]
        public static object? Speak(string? args)
        {
            string msg = string.Empty;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("message", out var mProp))
                        msg = mProp.GetString() ?? "";
                    else if (doc.RootElement.TryGetProperty("text", out var tProp))
                        msg = tProp.GetString() ?? "";
                }
                catch
                {
                    msg = args.Trim();
                }
            }

            return WebApiService.SubmitSpeak(msg);
        }

        [SocketAPIEndpoint]
        public static object? ListPresets(string? args)
        {
            return WebApiService.ListPresets();
        }

        [SocketAPIEndpoint]
        public static object? ListVillagers(string? args)
        {
            return WebApiService.ListVillagers();
        }
    }
}
