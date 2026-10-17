using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
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
        public static object? GetGameTime(string? args)
        {
            return WebApiService.GetGameTime();
        }

        [SocketAPIEndpoint]
        public static object? ToggleRequests(string? args)
        {
            bool? val = ParseBoolArg(args, "requests", "accepting_commands");
            return WebApiService.ToggleRequests(val);
        }

        [SocketAPIEndpoint]
        public static object? ToggleMashB(string? args)
        {
            bool? val = ParseBoolArg(args, "mash_b", "mashb");
            return WebApiService.ToggleMashB(val);
        }

        [SocketAPIEndpoint]
        public static object? ToggleRefresh(string? args)
        {
            bool? val = ParseBoolArg(args, "refresh", "refresh_map");
            return WebApiService.ToggleRefresh(val);
        }

        [SocketAPIEndpoint]
        public static object? StartBot(string? args)
        {
            return WebApiService.StartBot();
        }

        [SocketAPIEndpoint]
        public static object? StopBot(string? args)
        {
            return WebApiService.StopBot();
        }

        [SocketAPIEndpoint]
        public static object? RestartConnection(string? args)
        {
            return WebApiService.RestartConnection();
        }

        [SocketAPIEndpoint]
        public static object? PauseRequests(string? args)
        {
            return WebApiService.PauseRequests();
        }

        [SocketAPIEndpoint]
        public static object? ResumeRequests(string? args)
        {
            return WebApiService.ResumeRequests();
        }

        [SocketAPIEndpoint]
        public static async Task<object?> SetScreen(string? args)
        {
            bool on = ParseBoolArg(args, "on", "screen", "state") ?? true;
            return await WebApiService.SetScreenAsync(on).ConfigureAwait(false);
        }

        [SocketAPIEndpoint]
        public static async Task<object?> DetachController(string? args)
        {
            return await WebApiService.DetachControllerAsync().ConfigureAwait(false);
        }

        [SocketAPIEndpoint]
        public static object? GetControlGuide(string? args)
        {
            return WebApiService.GetControlGuide();
        }

        [SocketAPIEndpoint]
        public static async Task<object?> Control(string? args)
        {
            string action = "";
            string? arg = null;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("action", out var aProp))
                        action = aProp.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("arg", out var arProp))
                        arg = arProp.GetString();
                    else if (doc.RootElement.TryGetProperty("value", out var vProp))
                        arg = vProp.GetString();
                }
                catch
                {
                    action = args.Trim();
                }
            }
            return await WebApiService.ExecuteControlActionAsync(action, arg).ConfigureAwait(false);
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

        [SocketAPIEndpoint]
        public static object? GetEvents(string? args)
        {
            int limit = 50;
            long? sinceId = null;
            string? type = null;

            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("limit", out var lProp) && lProp.TryGetInt32(out var l))
                        limit = Math.Clamp(l, 1, 200);
                    if (doc.RootElement.TryGetProperty("since_id", out var sProp) && sProp.TryGetInt64(out var s))
                        sinceId = s;
                    if (doc.RootElement.TryGetProperty("type", out var tProp))
                        type = tProp.GetString();
                }
                catch { }
            }

            return WebApiService.GetEvents(limit, sinceId, type);
        }

        [SocketAPIEndpoint]
        public static object? RequestDodoRefresh(string? args)
        {
            int delay = 0;
            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("delay_seconds", out var dProp) && dProp.TryGetInt32(out var dp))
                        delay = dp;
                    else if (doc.RootElement.TryGetProperty("delay", out var dlProp) && dlProp.TryGetInt32(out var dlp))
                        delay = dlp;
                }
                catch
                {
                    if (int.TryParse(args.Trim(), out var d))
                        delay = d;
                }
            }
            return WebApiService.RequestDodoRefresh(delay);
        }

        [SocketAPIEndpoint]
        public static object? NewDodo(string? args) => RequestDodoRefresh(args);

        [SocketAPIEndpoint]
        public static async Task<object?> Click(string? args)
        {
            string? button = "B";
            int count = 1;
            int delayMs = 200;

            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("button", out var bProp))
                        button = bProp.GetString();
                    if (doc.RootElement.TryGetProperty("count", out var cProp) && cProp.TryGetInt32(out var c))
                        count = c;
                    if (doc.RootElement.TryGetProperty("delay_ms", out var dProp) && dProp.TryGetInt32(out var d))
                        delayMs = d;
                }
                catch
                {
                    button = args.Trim();
                }
            }
            return await WebApiService.SendButtonClickAsync(button, count, delayMs).ConfigureAwait(false);
        }

        [SocketAPIEndpoint]
        public static object? SearchItems(string? args)
        {
            string query = string.Empty;
            int limit = 20;
            string language = "en";

            if (!string.IsNullOrWhiteSpace(args))
            {
                try
                {
                    using var doc = JsonDocument.Parse(args);
                    if (doc.RootElement.TryGetProperty("query", out var qProp))
                        query = qProp.GetString() ?? "";
                    else if (doc.RootElement.TryGetProperty("q", out var qpProp))
                        query = qpProp.GetString() ?? "";

                    if (doc.RootElement.TryGetProperty("limit", out var lProp) && lProp.TryGetInt32(out var l))
                        limit = l;
                    if (doc.RootElement.TryGetProperty("language", out var langProp))
                        language = langProp.GetString() ?? "en";
                }
                catch
                {
                    query = args.Trim();
                }
            }
            return WebApiService.SearchItems(query, limit, language);
        }

        private static bool? ParseBoolArg(string? args, params string[] propertyNames)
        {
            if (string.IsNullOrWhiteSpace(args))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(args);
                foreach (var name in propertyNames)
                {
                    if (doc.RootElement.TryGetProperty(name, out var p))
                    {
                        if (p.ValueKind == JsonValueKind.True) return true;
                        if (p.ValueKind == JsonValueKind.False) return false;
                        if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var sb)) return sb;
                    }
                }
                if (doc.RootElement.TryGetProperty("value", out var vp))
                {
                    if (vp.ValueKind == JsonValueKind.True) return true;
                    if (vp.ValueKind == JsonValueKind.False) return false;
                    if (vp.ValueKind == JsonValueKind.String && bool.TryParse(vp.GetString(), out var sb)) return sb;
                }
                if (doc.RootElement.TryGetProperty("enabled", out var ep))
                {
                    if (ep.ValueKind == JsonValueKind.True) return true;
                    if (ep.ValueKind == JsonValueKind.False) return false;
                    if (ep.ValueKind == JsonValueKind.String && bool.TryParse(ep.GetString(), out var sb)) return sb;
                }
            }
            catch
            {
                var trimmed = args.Trim();
                if (bool.TryParse(trimmed, out var tb)) return tb;
                if (trimmed == "1" || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (trimmed == "0" || trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }

            return null;
        }
    }
}
