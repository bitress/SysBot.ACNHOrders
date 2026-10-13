using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NHSE.Core;
using NHSE.Villagers;
using SysBot.Base;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders.WebAPI
{
    public static class WebApiService
    {
        public static object GetStatus()
        {
            if (Globals.Bot == null)
            {
                return new
                {
                    success = false,
                    is_running = false,
                    message = "Bot is not running."
                };
            }

            var bot = Globals.Bot;
            var isDropMode = bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode;
            var visitorCount = bot.VisitorList.VisitorCount > 0 ? bot.VisitorList.VisitorCount - 1 : 0;

            var visitorsList = new List<string>();
            try
            {
                var vString = bot.VisitorList.VisitorFormattedString;
                if (!string.IsNullOrWhiteSpace(vString))
                {
                    visitorsList = vString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();
                }
            }
            catch { }

            var isDodoValid = bot.DodoPosition.IsDodoValid(bot.DodoCode);
            var isConnected = bot.Connection?.Connected ?? false;
            var consoleStage = Globals.ConsoleControl?.Stage.ToString() ?? (isConnected ? "Running" : "Stopped");

            return new
            {
                success = true,
                is_running = true,
                is_connected = isConnected,
                console_stage = consoleStage,
                mode = isDropMode ? "DropMode" : "OrderMode",
                is_drop_mode = isDropMode,
                is_order_mode = !isDropMode,
                island_name = bot.TownName,
                dodo_code = isDropMode ? bot.DodoCode : (isDodoValid ? bot.DodoCode : null),
                is_dodo_valid = isDodoValid,
                is_refreshing_dodo = !isDodoValid,
                layer = bot.CLayer,
                visitors_count = visitorCount,
                visitors = bot.VisitorList.VisitorFormattedString,
                visitor_list = visitorsList,
                last_arrival = IslandEventFeed.Instance.LastArrival,
                last_departure = IslandEventFeed.Instance.LastDeparture,
                recent_events = IslandEventFeed.Instance.GetRecent(10),
                is_dirty = bot.GameIsDirty,
                accepting_commands = bot.Config.AcceptingCommands,
                queue_count = Globals.Hub?.Orders?.Count ?? 0,
                battery_charge = bot.ChargePercent,
                last_dodo_fetch = bot.LastDodoFetchTime.ToString("yyyy-MM-dd HH:mm:ss"),
                game_time = bot.LastTimeState.ToString(),
                is_nooks_open = !(bot.LastTimeState.Hour >= 22 || bot.LastTimeState.Hour < 8),
                server_time = DateTime.UtcNow.ToString("o")
            };
        }

        public static object GetGameTime()
        {
            if (Globals.Bot == null)
            {
                return new
                {
                    success = false,
                    message = "Bot is not running."
                };
            }

            var bot = Globals.Bot;
            var time = bot.LastTimeState;
            var isDropMode = bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode;
            var isNooksOpen = !(time.Hour >= 22 || time.Hour < 8);
            var nooksMessage = isNooksOpen ? "Nook's Cranny is expected to be open." : "Nook's Cranny is closed.";
            var message = isDropMode
                ? $"The current in-game time is: {time} ({nooksMessage})"
                : $"Last order started at: {time}";

            return new
            {
                success = true,
                game_time = time.ToString(),
                formatted = time.ToString(),
                year = (int)time.Year,
                month = (int)time.Month,
                day = (int)time.Day,
                hour = (int)time.Hour,
                minute = (int)time.Minute,
                is_nooks_open = isNooksOpen,
                nooks_status = isNooksOpen ? "open" : "closed",
                nooks_message = nooksMessage,
                mode = isDropMode ? "DropMode" : "OrderMode",
                message = message
            };
        }

        public static object GetDodo(string? orderId = null, string? userId = null)
        {
            if (Globals.Bot == null)
            {
                return new
                {
                    success = false,
                    dodo_code = (string?)null,
                    message = "Bot is not running."
                };
            }

            var bot = Globals.Bot;
            var isDropMode = bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode;

            // In Drop Mode / Treasure Island mode, return live Dodo code
            if (isDropMode)
            {
                var dodo = bot.DodoCode;
                var valid = bot.DodoPosition.IsDodoValid(dodo);
                return new
                {
                    success = true,
                    mode = "DropMode",
                    dodo_code = dodo,
                    is_valid = valid,
                    island_name = bot.TownName,
                    layer = bot.CLayer,
                    visitors_count = bot.VisitorList.VisitorCount > 0 ? bot.VisitorList.VisitorCount - 1 : 0,
                    message = valid ? $"Dodo code for {bot.TownName} is {dodo}" : "Dodo code is being refreshed or unavailable."
                };
            }

            // In Order Mode, check specific order if id is provided
            var targetId = !string.IsNullOrWhiteSpace(orderId) ? orderId : userId;
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                if (WebOrderRegistry.TryGet(targetId, out var webOrder) && webOrder != null)
                {
                    var pos = Globals.Hub.Orders.GetPosition(webOrder.UserGuid);
                    var eta = pos > 0 ? QueueExtensions.GetETA(pos) : (webOrder.Status == "ready" ? "00m:00s" : "01m:00s");
                    return new
                    {
                        success = true,
                        mode = "OrderMode",
                        order_id = webOrder.OrderIdString,
                        user_name = webOrder.Trader,
                        villager_name = webOrder.VillagerName,
                        status = webOrder.Status,
                        queue_position = pos > 0 ? pos : (webOrder.Status == "ready" ? 0 : -1),
                        eta = eta,
                        dodo_code = webOrder.DodoCode,
                        island_name = bot.TownName,
                        message = webOrder.StatusMessage
                    };
                }
            }

            // Default order mode response
            return new
            {
                success = true,
                mode = "OrderMode",
                island_name = bot.TownName,
                active_user = bot.CurrentUserName,
                dodo_code = bot.DodoPosition.IsDodoValid(bot.DodoCode) ? bot.DodoCode : null,
                queue_count = Globals.Hub?.Orders?.Count ?? 0,
                message = "Submit an order to receive a dedicated Dodo code when ready."
            };
        }

        public static object GetQueue()
        {
            if (Globals.Hub?.Orders == null)
            {
                return new
                {
                    success = false,
                    count = 0,
                    orders = Array.Empty<object>(),
                    message = "Queue hub is not initialized."
                };
            }

            var orders = Globals.Hub.Orders.ToArray();
            var orderList = new List<object>();

            for (int i = 0; i < orders.Length; i++)
            {
                var ord = orders[i];
                var pos = i + 1;
                var eta = QueueExtensions.GetETA(pos);
                var isWeb = ord is WebOrderRequest<Item>;
                var webOrd = ord as WebOrderRequest<Item>;

                orderList.Add(new
                {
                    position = pos,
                    order_id = webOrd?.OrderIdString ?? ord.OrderID.ToString(),
                    user_guid = ord.UserGuid.ToString(),
                    username = ord.VillagerName,
                    villager = ord.VillagerOrder?.GameName ?? "",
                    item_count = ord.Order?.Length ?? 0,
                    status = webOrd?.Status ?? (pos == 1 ? "next" : "queued"),
                    eta = eta,
                    created_at = webOrd?.CreatedAt.ToString("o")
                });
            }

            return new
            {
                success = true,
                count = orderList.Count,
                island_name = Globals.Bot?.TownName ?? "",
                current_active_user = Globals.Bot?.CurrentUserName ?? "",
                orders = orderList
            };
        }

        public static object GetOrderStatus(string idOrUser)
        {
            if (string.IsNullOrWhiteSpace(idOrUser))
            {
                return new
                {
                    success = false,
                    found = false,
                    message = "Order ID or User ID is required."
                };
            }

            if (WebOrderRegistry.TryGet(idOrUser, out var webOrder) && webOrder != null)
            {
                var pos = Globals.Hub.Orders.GetPosition(webOrder.UserGuid);
                var eta = pos > 0 ? QueueExtensions.GetETA(pos) : (webOrder.Status == "ready" ? "00m:00s" : "00m:00s");
                return new
                {
                    success = true,
                    found = true,
                    order_id = webOrder.OrderIdString,
                    user_guid = webOrder.UserGuid.ToString(),
                    username = webOrder.Trader,
                    villager_name = webOrder.VillagerName,
                    item_count = webOrder.Order?.Length ?? 0,
                    status = webOrder.Status,
                    queue_position = pos > 0 ? pos : (webOrder.Status == "ready" ? 0 : -1),
                    eta = eta,
                    estimated_seconds = pos > 0 ? (pos * 120) : 0,
                    dodo_code = webOrder.DodoCode,
                    island_name = Globals.Bot?.TownName ?? "Sinta",
                    message = webOrder.StatusMessage,
                    created_at = webOrder.CreatedAt.ToString("o"),
                    updated_at = webOrder.UpdatedAt.ToString("o")
                };
            }

            // Check if user is in Hub orders even if not registered via WebOrderRegistry
            if (ulong.TryParse(idOrUser.Trim(), out var ulongId))
            {
                var pos = Globals.Hub.Orders.GetPosition(ulongId);
                var rawOrder = Globals.Hub.Orders.GetByUserId(ulongId);
                if (pos > 0 && rawOrder != null)
                {
                    var eta = QueueExtensions.GetETA(pos);
                    return new
                    {
                        success = true,
                        found = true,
                        order_id = rawOrder.OrderID.ToString(),
                        user_guid = rawOrder.UserGuid.ToString(),
                        username = rawOrder.VillagerName,
                        villager_name = rawOrder.VillagerOrder?.GameName ?? "",
                        item_count = rawOrder.Order?.Length ?? 0,
                        status = "queued",
                        queue_position = pos,
                        eta = eta,
                        estimated_seconds = pos * 120,
                        dodo_code = (string?)null,
                        island_name = Globals.Bot?.TownName ?? "Sinta",
                        message = $"In queue at position {pos}."
                    };
                }
            }

            return new
            {
                success = false,
                found = false,
                order_id = idOrUser,
                status = "not_found",
                queue_position = -1,
                dodo_code = (string?)null,
                island_name = Globals.Bot?.TownName ?? "Sinta",
                message = "Order not found in queue or has already expired."
            };
        }

        public static object SubmitOrder(
            string? orderText,
            string? villagerName = null,
            string? presetName = null,
            string? username = null,
            string? orderId = null,
            ulong? userId = null,
            List<string>? itemsList = null)
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            var bot = Globals.Bot;
            var cfg = bot.Config;

            if (!cfg.AcceptingCommands)
                return new { success = false, message = "Bot is not currently accepting order commands." };

            if (cfg.DodoModeConfig.LimitedDodoRestoreOnlyMode)
                return new { success = false, message = "Bot is currently running in Drop/Dodo Restore mode. Orders are disabled." };

            var player = string.IsNullOrWhiteSpace(username) ? "WebUser" : username.Trim();
            var finalOrderId = string.IsNullOrWhiteSpace(orderId) ? GenerateShortId() : orderId.Trim();
            var userGuid = userId ?? GenerateUserGuid(finalOrderId, player);
            var orderNumericId = QueueExtensions.GetNextID();

            // Check if user is already queued
            if (Globals.Hub.Orders.GetByUserId(userGuid) != null)
            {
                var existingPos = Globals.Hub.Orders.GetPosition(userGuid);
                return new
                {
                    success = false,
                    message = $"User is already in queue at position {existingPos}.",
                    order_id = finalOrderId,
                    queue_position = existingPos
                };
            }

            // Check if currently processing
            if (bot.CurrentUserName == player || bot.CurrentUserId == userGuid)
            {
                return new
                {
                    success = false,
                    message = "Failed to queue order as it is currently being processed."
                };
            }

            // Handle Villager
            VillagerRequest? villagerRequest = null;
            var combinedOrder = (orderText ?? "").Trim();

            if (itemsList != null && itemsList.Count > 0)
            {
                combinedOrder = string.Join(", ", itemsList);
            }

            // Check explicit villager param or extracted from order text
            if (!string.IsNullOrWhiteSpace(villagerName))
            {
                if (!cfg.AllowVillagerInjection)
                    return new { success = false, message = "Villager injection is currently disabled on this bot." };

                var vName = villagerName.Trim();
                if (!VillagerResources.IsVillagerDataKnown(vName))
                    vName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, vName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (string.IsNullOrWhiteSpace(vName) || VillagerOrderParser.IsUnadoptable(vName))
                    return new { success = false, message = $"Villager '{villagerName}' is not adoptable or unknown." };

                var replace = VillagerResources.GetVillager(vName);
                villagerRequest = new VillagerRequest(player, replace, 0, GameInfo.Strings.GetVillager(vName));
            }
            else if (!string.IsNullOrWhiteSpace(combinedOrder))
            {
                var vResult = VillagerOrderParser.ExtractVillagerName(combinedOrder, out var res, out var san);
                if (vResult == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
                    return new { success = false, message = $"Invalid villager requested: {res}" };

                if (vResult == VillagerOrderParser.VillagerRequestResult.Success)
                {
                    if (!cfg.AllowVillagerInjection)
                        return new { success = false, message = "Villager injection is currently disabled." };

                    combinedOrder = san;
                    var replace = VillagerResources.GetVillager(res);
                    villagerRequest = new VillagerRequest(player, replace, 0, GameInfo.Strings.GetVillager(res));
                }
            }

            // Handle Preset vs Items
            Item[]? items = null;

            if (!string.IsNullOrWhiteSpace(presetName))
            {
                var loadedPreset = PresetLoader.GetPreset(cfg.OrderConfig, presetName.Trim());
                if (loadedPreset == null)
                    return new { success = false, message = $"Preset '{presetName}' was not found." };
                items = loadedPreset;
            }
            else if (!string.IsNullOrWhiteSpace(combinedOrder))
            {
                items = ItemParser.GetItemsFromUserInput(combinedOrder, cfg.DropConfig, ItemDestination.FieldItemDropped).ToArray();
            }
            else
            {
                items = new Item[1] { new Item(Item.NONE) };
            }

            // Validate sanity of items
            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, cfg.DropConfig))
            {
                var unsafeList = InternalItemTool.CurrentInstance.GetUnsafeItemNames(items);
                return new
                {
                    success = false,
                    message = $"Order contains unsafe items that could damage save data: {string.Join(", ", unsafeList)}"
                };
            }

            // Create WebOrderRequest
            var webOrderReq = new WebOrderRequest<Item>(
                items,
                userGuid,
                orderNumericId,
                finalOrderId,
                player,
                player,
                villagerRequest
            );

            Globals.Hub.Orders.Enqueue(webOrderReq);
            WebOrderRegistry.Register(webOrderReq);

            var position = Globals.Hub.Orders.Count;
            var eta = QueueExtensions.GetETA(position);

            LogUtil.LogInfo($"Web order '{finalOrderId}' for '{player}' queued at position {position}. Items: {items.Length}", "WebAPI");

            return new
            {
                success = true,
                order_id = finalOrderId,
                user_guid = userGuid.ToString(),
                queue_position = position,
                eta = eta,
                estimated_seconds = position * 120,
                status = "queued",
                island_name = bot.TownName,
                item_count = items.Length,
                villager = villagerRequest?.GameName ?? "",
                message = $"Successfully added to queue at position {position}. Predicted ETA: {eta}."
            };
        }

        public static object CancelOrder(string idOrUser)
        {
            if (string.IsNullOrWhiteSpace(idOrUser))
                return new { success = false, message = "Order ID or User ID is required." };

            var cancelled = WebOrderRegistry.Cancel(idOrUser);
            if (!cancelled && ulong.TryParse(idOrUser.Trim(), out var ulongId))
            {
                cancelled = Globals.Hub.Orders.RemoveByUserId(ulongId);
            }

            return new
            {
                success = cancelled,
                order_id = idOrUser,
                message = cancelled ? "Order successfully cancelled and removed from queue." : "Order not found in queue."
            };
        }

        public static object SubmitDrop(
            string? itemsText,
            string? dropType = "items",
            string? username = "WebUser",
            int? maxCount = null,
            List<string>? itemsList = null)
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            var bot = Globals.Bot;
            var cfg = bot.Config;

            if (!cfg.AcceptingCommands)
                return new { success = false, message = "Bot is not currently accepting drop commands." };

            if (!cfg.AllowDrop)
                return new { success = false, message = "AllowDrop is currently disabled in configuration." };

            var combinedText = (itemsText ?? "").Trim();
            if (itemsList != null && itemsList.Count > 0)
                combinedText = string.Join(", ", itemsList);

            if (string.IsNullOrWhiteSpace(combinedText))
                return new { success = false, message = "No items specified to drop." };

            Item[] items;
            var type = (dropType ?? "items").ToLowerInvariant();

            if (type == "diy" || type == "recipe")
            {
                items = ItemParser.GetDIYsFromUserInput(combinedText).ToArray();
            }
            else
            {
                items = ItemParser.GetItemsFromUserInput(combinedText, cfg.DropConfig, cfg.DropConfig.UseLegacyDrop ? ItemDestination.PlayerDropped : ItemDestination.HeldItem).ToArray();
            }

            if (items.Length == 0)
                return new { success = false, message = "Could not parse any valid items from request." };

            MultiItem.StackToMax(items);

            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, cfg.DropConfig))
            {
                var unsafeList = InternalItemTool.CurrentInstance.GetUnsafeItemNames(items);
                return new
                {
                    success = false,
                    message = $"Attempted to drop unsafe items: {string.Join(", ", unsafeList)}"
                };
            }

            var limit = maxCount ?? cfg.DropConfig.MaxDropCount;
            if (items.Length > limit)
                items = items.Take(limit).ToArray();

            var dropRequest = new ItemRequest(string.IsNullOrWhiteSpace(username) ? "WebUser" : username.Trim(), items);
            bot.Injections.Enqueue(dropRequest);

            LogUtil.LogInfo($"Web drop request queued ({items.Length} items) by '{username}'", "WebAPI");

            return new
            {
                success = true,
                dropped_count = items.Length,
                island_name = bot.TownName,
                message = $"{items.Length} item drop request(s) queued and will execute momentarily."
            };
        }

        public static object SubmitClean()
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            if (!Globals.Bot.Config.AllowClean)
                return new { success = false, message = "Clean functionality is disabled in configuration." };

            Globals.Bot.CleanRequested = true;
            LogUtil.LogInfo("Clean requested via WebAPI", "WebAPI");

            return new
            {
                success = true,
                message = "Clean request queued and will be executed momentarily."
            };
        }

        public static object SubmitTurnips(int price)
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            if (price < 0)
                price = 0;

            Globals.Bot.StonkRequests.Enqueue(new TurnipRequest("WebUser", price));
            LogUtil.LogInfo($"Turnip price set to {price} via WebAPI", "WebAPI");

            return new
            {
                success = true,
                price = price,
                message = $"Turnip stonk value queued to be set to {price}."
            };
        }

        public static object SubmitSpeak(string message)
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            if (string.IsNullOrWhiteSpace(message))
                return new { success = false, message = "Message is empty." };

            Globals.Bot.Speaks.Enqueue(new SpeakRequest("WebUser", message.Trim()));
            LogUtil.LogInfo($"In-game chat queued: '{message}'", "WebAPI");

            return new
            {
                success = true,
                message = $"Speak message queued: '{message}'"
            };
        }

        public static object ListPresets()
        {
            if (Globals.Bot == null)
                return new { success = false, presets = Array.Empty<string>(), message = "Bot is not running." };

            var cfg = Globals.Bot.Config.OrderConfig;
            if (!Directory.Exists(cfg.NHIPresetsDirectory))
                return new { success = true, presets = Array.Empty<string>() };

            var presets = PresetLoader.GetPresets(cfg);
            return new
            {
                success = true,
                count = presets.Length,
                presets = presets
            };
        }

        public static object ListVillagers()
        {
            if (Globals.Bot?.Villagers == null)
                return new { success = false, villagers = Array.Empty<string>(), message = "Villagers not loaded." };

            var vString = Globals.Bot.Villagers.LastVillagers;
            var list = string.IsNullOrWhiteSpace(vString)
                ? new List<string>()
                : vString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            return new
            {
                success = true,
                count = list.Count,
                island_name = Globals.Bot.TownName,
                villagers = list
            };
        }

        public static object InjectVillagers(List<(string VillagerName, int? PlotNumber)> requests)
        {
            if (Globals.Bot == null)
                return new { success = false, message = "Bot is not running." };

            var bot = Globals.Bot;
            if (bot.Villagers == null)
                return new { success = false, message = "Villager system is not initialized." };

            if (!bot.Config.AllowVillagerInjection)
                return new { success = false, message = "Villager injection is disabled in the bot configuration (AllowVillagerInjection is false)." };

            if (requests == null || requests.Count == 0)
                return new { success = false, message = "No villagers provided." };

            var injected = new List<object>();
            var errors = new List<string>();

            var usedPlots = new HashSet<int>();
            foreach (var req in requests)
            {
                if (req.PlotNumber.HasValue)
                {
                    int p = req.PlotNumber.Value;
                    if (p >= 1 && p <= 10) p -= 1;
                    if (p >= 0 && p <= 9) usedPlots.Add(p);
                }
            }

            int nextAutoPlot = 0;

            foreach (var (rawName, reqPlot) in requests)
            {
                if (string.IsNullOrWhiteSpace(rawName))
                    continue;

                var vName = rawName.Trim();
                if (!VillagerResources.IsVillagerDataKnown(vName))
                    vName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, vName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (string.IsNullOrWhiteSpace(vName) || VillagerOrderParser.IsUnadoptable(vName))
                {
                    errors.Add($"Villager '{rawName}' is unknown or not adoptable.");
                    continue;
                }

                int targetPlot;
                if (reqPlot.HasValue)
                {
                    int p = reqPlot.Value;
                    if (p >= 1 && p <= 10) p -= 1;
                    if (p < 0 || p > 9)
                    {
                        errors.Add($"Invalid plot number {reqPlot.Value} for '{rawName}'. Must be between 1 and 10.");
                        continue;
                    }
                    targetPlot = p;
                }
                else
                {
                    while (nextAutoPlot < 10 && usedPlots.Contains(nextAutoPlot))
                        nextAutoPlot++;
                    if (nextAutoPlot >= 10)
                    {
                        errors.Add($"No available plots remaining (all 10 occupied or targeted) for '{rawName}'.");
                        continue;
                    }
                    targetPlot = nextAutoPlot;
                    usedPlots.Add(targetPlot);
                    nextAutoPlot++;
                }

                var replace = VillagerResources.GetVillager(vName);
                var gameName = GameInfo.Strings.GetVillager(vName);
                var vr = new VillagerRequest("WebAPI", replace, (byte)targetPlot, gameName);

                bot.VillagerInjections.Enqueue(vr);

                injected.Add(new
                {
                    villager = gameName,
                    internal_name = vName,
                    plot = targetPlot + 1,
                    house_index = targetPlot
                });
            }

            return new
            {
                success = injected.Count > 0,
                injected_count = injected.Count,
                injected = injected,
                errors = errors,
                queue_length = bot.VillagerInjections.Count,
                message = injected.Count > 0
                    ? $"Successfully queued {injected.Count} villager(s) for injection into plots."
                    : "No villagers could be queued. See errors."
            };
        }

        public static object GetEvents(int limit = 50, long? sinceId = null, string? type = null)
        {
            var events = IslandEventFeed.Instance.GetEvents(limit, sinceId, type);
            return new
            {
                success = true,
                events = events,
                count = events.Count,
                total_recorded = IslandEventFeed.Instance.TotalCount,
                last_arrival = IslandEventFeed.Instance.LastArrival,
                last_departure = IslandEventFeed.Instance.LastDeparture,
                server_time = DateTime.UtcNow.ToString("o")
            };
        }

        public static object RequestDodoRefresh(int delaySeconds = 0)
        {
            if (Globals.Bot == null)
            {
                return new
                {
                    success = false,
                    message = "Bot is not running."
                };
            }

            var bot = Globals.Bot;
            if (delaySeconds > 0)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delaySeconds * 1000).ConfigureAwait(false);
                    if (Globals.Bot != null)
                        Globals.Bot.RestoreRestartRequested = true;
                });
                IslandEventFeed.Instance.RecordEvent("system", $"Dodo refresh scheduled in {delaySeconds}s for {bot.TownName}");
                return new
                {
                    success = true,
                    island_name = bot.TownName,
                    delay_seconds = delaySeconds,
                    message = $"Dodo refresh scheduled in {delaySeconds} seconds."
                };
            }

            bot.RestoreRestartRequested = true;
            IslandEventFeed.Instance.RecordEvent("system", $"Dodo refresh requested via WebAPI for {bot.TownName}");
            return new
            {
                success = true,
                island_name = bot.TownName,
                message = "Dodo refresh requested. Bot will restart the game and fetch a new Dodo code."
            };
        }

        public static async Task<object> SendButtonClickAsync(string? buttonName, int count = 1, int delayMs = 200)
        {
            if (Globals.Bot == null || Globals.Bot.Connection == null || !Globals.Bot.Connection.Connected)
            {
                return new
                {
                    success = false,
                    message = "Bot Switch connection is offline or unavailable."
                };
            }

            var bot = Globals.Bot;
            var bName = (buttonName ?? "B").Trim().ToUpperInvariant();

            if (bName == "DETACH")
            {
                await bot.Connection.SendAsync(SwitchCommand.DetachController(), CancellationToken.None).ConfigureAwait(false);
                IslandEventFeed.Instance.RecordEvent("system", "Virtual controller detached via WebAPI.");
                return new
                {
                    success = true,
                    action = "detach",
                    message = "Virtual controller detached."
                };
            }

            if (bName == "UNSTICK" || bName == "MASH_B" || bName == "MASHB")
            {
                int mCount = Math.Clamp(count > 1 ? count : 5, 1, 20);
                for (int i = 0; i < mCount; i++)
                {
                    await bot.Connection.SendAsync(SwitchCommand.Click(SwitchButton.B, bot.UseCRLF), CancellationToken.None).ConfigureAwait(false);
                    await Task.Delay(200).ConfigureAwait(false);
                }
                IslandEventFeed.Instance.RecordEvent("system", $"Unstick executed: Mashed B {mCount} times.");
                return new
                {
                    success = true,
                    button = "B",
                    count = mCount,
                    message = $"Unstick executed: Mashed B {mCount} times."
                };
            }

            if (!Enum.TryParse<SwitchButton>(bName, true, out var button))
            {
                return new
                {
                    success = false,
                    message = $"Unknown button '{buttonName}'. Valid buttons: A, B, X, Y, L, R, ZL, ZR, PLUS, MINUS, DUP, DDOWN, DLEFT, DRIGHT, HOME, CAPTURE, UNSTICK, DETACH."
                };
            }

            count = Math.Clamp(count, 1, 20);
            delayMs = Math.Clamp(delayMs, 50, 5000);

            for (int i = 0; i < count; i++)
            {
                await bot.Connection.SendAsync(SwitchCommand.Click(button, bot.UseCRLF), CancellationToken.None).ConfigureAwait(false);
                if (i < count - 1)
                    await Task.Delay(delayMs).ConfigureAwait(false);
            }

            IslandEventFeed.Instance.RecordEvent("system", $"Button {button} clicked {count} time(s).");
            return new
            {
                success = true,
                button = button.ToString(),
                count = count,
                delay_ms = delayMs,
                message = $"Successfully clicked {button} {count} time(s)."
            };
        }

        public static object SearchItems(string? query, int limit = 20, string language = "en")
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return new
                {
                    success = false,
                    error = "Query parameter must be at least 2 characters.",
                    items = Array.Empty<object>()
                };
            }

            query = query.Trim();
            limit = Math.Clamp(limit, 1, 100);

            if (!OrderPreparation.Languages.Contains(language))
                language = "en";

            var results = ItemSearchService.Search(query, language)
                .Take(limit)
                .Select(item => new
                {
                    name = item.Text,
                    id = item.Value,
                    hex = item.Value.ToString("X4")
                })
                .ToList();

            return new
            {
                success = true,
                query = query,
                language = language,
                count = results.Count,
                items = results
            };
        }

        private static string GenerateShortId()
        {
            return "web_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static ulong GenerateUserGuid(string orderId, string username)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"{orderId}_{username}_{DateTime.UtcNow.Ticks}"));
            return BitConverter.ToUInt64(hash, 0);
        }
    }
}
