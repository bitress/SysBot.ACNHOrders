using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocketAPI;
using SysBot.Base;

namespace SysBot.ACNHOrders.WebAPI
{
    /// <summary>
    /// Lightweight, asynchronous HTTP REST API server with full CORS support.
    /// Allows browser web applications and backend services to interact with SysBot.
    /// </summary>
    public sealed class HttpApiServer
    {
        private HttpListener? _listener;
        private CancellationTokenSource _cts = new();
        private SocketAPIServerConfig _config = new();

        private static HttpApiServer? _instance;
        public static HttpApiServer Instance => _instance ??= new HttpApiServer();

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private HttpApiServer() { }

        public async Task Start(SocketAPIServerConfig config)
        {
            _config = config;
            if (!config.HttpApiEnabled)
            {
                LogUtil.LogInfo("HTTP REST API server is disabled in configuration.", "HttpAPI");
                return;
            }

            _listener = new HttpListener();
            var port = config.HttpPort;

            // Attempt wildcard prefix, fallback to localhost if unauthorized
            var primaryPrefix = $"http://*:{port}/";
            var fallbackPrefix = $"http://localhost:{port}/";
            var loopbackPrefix = $"http://127.0.0.1:{port}/";

            bool started = false;
            try
            {
                _listener.Prefixes.Add(primaryPrefix);
                _listener.Start();
                started = true;
                LogUtil.LogInfo($"HTTP REST API server listening on {primaryPrefix}", "HttpAPI");
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"Could not bind {primaryPrefix} ({ex.Message}), falling back to localhost...", "HttpAPI");
                try
                {
                    _listener.Close();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(fallbackPrefix);
                    _listener.Prefixes.Add(loopbackPrefix);
                    _listener.Start();
                    started = true;
                    LogUtil.LogInfo($"HTTP REST API server listening on {fallbackPrefix} and {loopbackPrefix}", "HttpAPI");
                }
                catch (Exception fallbackEx)
                {
                    LogUtil.LogError($"Failed to start HTTP REST API server: {fallbackEx.Message}", "HttpAPI");
                    return;
                }
            }

            if (!started)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && _listener.IsListening)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync().ConfigureAwait(false);
                        _ = ProcessRequestAsync(context);
                    }
                    catch (HttpListenerException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogUtil.LogError($"HTTP request handling error: {ex.Message}", "HttpAPI");
                    }
                }
            }, token);
        }

        public void Stop()
        {
            try
            {
                _cts.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Apply CORS headers to all responses
            response.Headers.Add("Access-Control-Allow-Origin", _config.CorsAllowOrigin ?? "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With, Accept");
            response.Headers.Add("Access-Control-Max-Age", "86400");

            // Handle preflight OPTIONS request
            if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
                response.Close();
                return;
            }

            // Check localhost restriction if enabled
            if (_config.AllowLocalhostOnly && request.RemoteEndPoint != null && !IPAddress.IsLoopback(request.RemoteEndPoint.Address))
            {
                await SendJsonResponseAsync(response, new { success = false, error = "Forbidden: Access restricted to localhost." }, 403).ConfigureAwait(false);
                return;
            }

            // Check API Key if configured
            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                var reqKey = request.Headers["X-API-Key"] ?? request.Headers["Authorization"]?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (!string.Equals(reqKey, _config.ApiKey, StringComparison.Ordinal))
                {
                    await SendJsonResponseAsync(response, new { success = false, error = "Unauthorized: Invalid or missing API key." }, 401).ConfigureAwait(false);
                    return;
                }
            }

            try
            {
                var path = (request.Url?.AbsolutePath ?? "/").TrimEnd('/');
                if (string.IsNullOrEmpty(path))
                    path = "/";

                var method = request.HttpMethod.ToUpperInvariant();
                object? result = null;
                int statusCode = 200;

                // Read request body for POST/PUT
                string body = string.Empty;
                if (request.HasEntityBody)
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var queryParams = request.QueryString;

                // Routing table
                switch (path.ToLowerInvariant())
                {
                    case "":
                    case "/":
                    case "/api":
                        result = new
                        {
                            service = "SysBot.ACNHOrders API",
                            version = "2.0",
                            status = "running",
                            endpoints = new[]
                            {
                                "GET  /api/status",
                                "GET  /api/dodo",
                                "GET  /api/queue",
                                "GET  /api/order/status?id=...",
                                "POST /api/order",
                                "POST /api/order/cancel",
                                "POST /api/drop",
                                "POST /api/clean",
                                "POST /api/turnips",
                                "POST /api/speak",
                                "GET  /api/presets",
                                "GET  /api/villagers"
                            }
                        };
                        break;

                    case "/api/status":
                    case "/status":
                        result = WebApiService.GetStatus();
                        break;

                    case "/api/dodo":
                    case "/dodo":
                        {
                            var orderId = queryParams["order_id"] ?? queryParams["id"];
                            var userId = queryParams["user_id"];
                            result = WebApiService.GetDodo(orderId, userId);
                        }
                        break;

                    case "/api/queue":
                    case "/queue":
                        result = WebApiService.GetQueue();
                        break;

                    case "/api/order/status":
                    case "/order/status":
                    case "/api/order/position":
                    case "/order/position":
                        {
                            var id = queryParams["id"] ?? queryParams["order_id"] ?? queryParams["user_id"] ?? queryParams["user"];
                            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(body))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(body);
                                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                                        id = idProp.GetString();
                                    else if (doc.RootElement.TryGetProperty("order_id", out var oidProp))
                                        id = oidProp.GetString();
                                }
                                catch { }
                            }
                            result = WebApiService.GetOrderStatus(id ?? "");
                        }
                        break;

                    case "/api/order":
                    case "/api/order/submit":
                    case "/order":
                    case "/order/submit":
                        if (method == "GET")
                        {
                            result = WebApiService.GetQueue();
                        }
                        else if (method == "POST")
                        {
                            result = HandleOrderSubmit(body);
                        }
                        else if (method == "DELETE")
                        {
                            var id = queryParams["id"] ?? queryParams["order_id"] ?? body;
                            result = WebApiService.CancelOrder(id);
                        }
                        break;

                    case "/api/order/cancel":
                    case "/order/cancel":
                        {
                            var id = queryParams["id"] ?? queryParams["order_id"];
                            if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(body))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(body);
                                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                                        id = idProp.GetString();
                                    else if (doc.RootElement.TryGetProperty("order_id", out var oidProp))
                                        id = oidProp.GetString();
                                }
                                catch
                                {
                                    id = body.Trim();
                                }
                            }
                            result = WebApiService.CancelOrder(id ?? "");
                        }
                        break;

                    case "/api/drop":
                    case "/drop":
                        if (method == "POST")
                        {
                            result = HandleDropSubmit(body);
                        }
                        else
                        {
                            result = new { success = false, message = "Use POST to submit drop requests." };
                            statusCode = 405;
                        }
                        break;

                    case "/api/clean":
                    case "/clean":
                        result = WebApiService.SubmitClean();
                        break;

                    case "/api/turnips":
                    case "/turnips":
                    case "/api/turnips/max":
                    case "/turnips/max":
                        {
                            int price = 999999999;
                            if (path.EndsWith("/max", StringComparison.OrdinalIgnoreCase))
                            {
                                price = 999999999;
                            }
                            else if (!string.IsNullOrWhiteSpace(queryParams["value"]) && int.TryParse(queryParams["value"], out var p))
                            {
                                price = p;
                            }
                            else if (!string.IsNullOrWhiteSpace(body))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(body);
                                    if (doc.RootElement.TryGetProperty("value", out var vProp) && vProp.TryGetInt32(out var vp))
                                        price = vp;
                                    else if (doc.RootElement.TryGetProperty("price", out var prProp) && prProp.TryGetInt32(out var prp))
                                        price = prp;
                                }
                                catch { }
                            }
                            result = WebApiService.SubmitTurnips(price);
                        }
                        break;

                    case "/api/speak":
                    case "/speak":
                        {
                            string msg = queryParams["msg"] ?? queryParams["message"] ?? "";
                            if (string.IsNullOrWhiteSpace(msg) && !string.IsNullOrWhiteSpace(body))
                            {
                                try
                                {
                                    using var doc = JsonDocument.Parse(body);
                                    if (doc.RootElement.TryGetProperty("message", out var mProp))
                                        msg = mProp.GetString() ?? "";
                                    else if (doc.RootElement.TryGetProperty("msg", out var msgProp))
                                        msg = msgProp.GetString() ?? "";
                                    else if (doc.RootElement.TryGetProperty("text", out var tProp))
                                        msg = tProp.GetString() ?? "";
                                }
                                catch
                                {
                                    msg = body.Trim();
                                }
                            }
                            result = WebApiService.SubmitSpeak(msg);
                        }
                        break;

                    case "/api/presets":
                    case "/presets":
                        result = WebApiService.ListPresets();
                        break;

                    case "/api/villagers":
                    case "/villagers":
                        result = WebApiService.ListVillagers();
                        break;

                    default:
                        statusCode = 404;
                        result = new { success = false, error = "Endpoint not found", path = path };
                        break;
                }

                await SendJsonResponseAsync(response, result, statusCode).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error in API handler: {ex.Message}\r\n{ex.StackTrace}", "HttpAPI");
                await SendJsonResponseAsync(response, new { success = false, error = ex.Message }, 500).ConfigureAwait(false);
            }
        }

        private static object HandleOrderSubmit(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new { success = false, message = "Request body is empty." };

            string? orderText = null;
            string? villagerName = null;
            string? presetName = null;
            string? username = null;
            string? orderId = null;
            ulong? userId = null;
            List<string>? itemsList = null;

            try
            {
                using var doc = JsonDocument.Parse(body);
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
                else if (root.TryGetProperty("command", out var cProp))
                {
                    var cmd = cProp.GetString() ?? "";
                    if (cmd.StartsWith("!order ", StringComparison.OrdinalIgnoreCase))
                        orderText = cmd.Substring(7);
                    else if (cmd.StartsWith("!ordercat ", StringComparison.OrdinalIgnoreCase))
                        orderText = cmd.Substring(10);
                    else if (cmd.StartsWith("!preset ", StringComparison.OrdinalIgnoreCase))
                        presetName = cmd.Substring(8);
                    else
                        orderText = cmd;
                }

                if (root.TryGetProperty("villager", out var vProp))
                    villagerName = vProp.GetString();

                if (root.TryGetProperty("preset", out var pProp))
                    presetName = pProp.GetString();

                if (root.TryGetProperty("username", out var uProp))
                    username = uProp.GetString();
                else if (root.TryGetProperty("player_name", out var pnProp))
                    username = pnProp.GetString();
                else if (root.TryGetProperty("trader", out var trProp))
                    username = trProp.GetString();

                if (root.TryGetProperty("order_id", out var oidProp))
                    orderId = oidProp.GetString();
                else if (root.TryGetProperty("id", out var idProp))
                    orderId = idProp.GetString();

                if (root.TryGetProperty("user_id", out var uidProp))
                {
                    if (uidProp.ValueKind == JsonValueKind.Number && uidProp.TryGetUInt64(out var uidNum))
                        userId = uidNum;
                    else if (uidProp.ValueKind == JsonValueKind.String && ulong.TryParse(uidProp.GetString(), out var uidParsed))
                        userId = uidParsed;
                }
            }
            catch (JsonException)
            {
                // Raw string fallback
                orderText = body.Trim();
            }

            return WebApiService.SubmitOrder(orderText, villagerName, presetName, username, orderId, userId, itemsList);
        }

        private static object HandleDropSubmit(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new { success = false, message = "Request body is empty." };

            string? itemsText = null;
            string? dropType = "items";
            string? username = "WebUser";
            int? maxCount = null;
            List<string>? itemsList = null;

            try
            {
                using var doc = JsonDocument.Parse(body);
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
                else if (root.TryGetProperty("command", out var cProp))
                {
                    var cmd = cProp.GetString() ?? "";
                    if (cmd.StartsWith("!drop ", StringComparison.OrdinalIgnoreCase))
                        itemsText = cmd.Substring(6);
                    else if (cmd.StartsWith("!diy ", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("!dropdiy ", StringComparison.OrdinalIgnoreCase))
                    {
                        dropType = "diy";
                        itemsText = cmd.Substring(cmd.IndexOf(' ') + 1);
                    }
                    else
                        itemsText = cmd;
                }

                if (root.TryGetProperty("type", out var tProp))
                    dropType = tProp.GetString() ?? "items";

                if (root.TryGetProperty("username", out var uProp))
                    username = uProp.GetString();

                if (root.TryGetProperty("count", out var cntProp) && cntProp.TryGetInt32(out var cVal))
                    maxCount = cVal;
            }
            catch (JsonException)
            {
                itemsText = body.Trim();
            }

            return WebApiService.SubmitDrop(itemsText, dropType, username, maxCount, itemsList);
        }

        private static async Task SendJsonResponseAsync(HttpListenerResponse response, object? data, int statusCode = 200)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";

            var json = JsonSerializer.Serialize(data, _jsonOpts);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;

            try
            {
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                response.OutputStream.Close();
            }
            catch { }
        }
    }
}
