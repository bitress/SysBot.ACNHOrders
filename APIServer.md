# SysBot.ACNHOrders — Web & Socket API Server

SysBot.ACNHOrders includes two high-performance API interfaces that start automatically alongside the bot:

| Interface | Default Port | Best For |
|---|---|---|
| **HTTP REST API** | `5202` | Websites (React / Next.js / Vue / plain HTML), Python backends, curl |
| **TCP Socket API** | `5201` | Discord bots, bot controllers, low-latency integrations |

Both APIs are **mode-aware** — they automatically reflect whether the bot is running in **Order Mode** or **Drop/Treasure Island Mode**, and enforce access rules accordingly.

---

## Table of Contents

1. [Setup & Configuration](#1-setup--configuration)
2. [Windows-Specific Notes](#2-windows-specific-notes)
3. [Authentication (Optional)](#3-authentication-optional)
4. [HTTP REST API Reference](#4-http-rest-api-reference)
5. [TCP Socket API Reference](#5-tcp-socket-api-reference)
6. [Mode-Aware Behavior](#6-mode-aware-behavior)
7. [Integration Examples](#7-integration-examples)
8. [Error Reference](#8-error-reference)

---

## 1. Setup & Configuration

### Step 1 — Locate `server.json`

On first launch, SysBot automatically creates `server.json` in the same folder as the executable (`SysBot.ACNHOrders.exe`). Open it with any text editor.

### Step 2 — Edit `server.json`

```json
{
  "Enabled": true,
  "LogsEnabled": true,
  "Port": 5201,
  "HttpApiEnabled": true,
  "HttpPort": 5202,
  "HttpPrefix": "http://*:5202/",
  "CorsAllowOrigin": "*",
  "AllowDropFromWeb": true,
  "AllowOrderFromWeb": true,
  "ApiKey": "",
  "AllowLocalhostOnly": false
}
```

### Step 3 — Configuration Field Reference

| Field | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `false` | Enables the **TCP Socket API** (port `Port`). Set to `true` to turn it on. |
| `LogsEnabled` | bool | `true` | Prints Socket/HTTP API log lines to the console window. |
| `Port` | number | `5201` | TCP port for the Socket API. |
| `HttpApiEnabled` | bool | `true` | Enables the **HTTP REST API** (port `HttpPort`). |
| `HttpPort` | number | `5202` | HTTP port for the REST API. |
| `HttpPrefix` | string | `http://*:5202/` | Internal binding prefix. See [Windows notes](#2-windows-specific-notes). |
| `CorsAllowOrigin` | string | `"*"` | CORS `Access-Control-Allow-Origin` header value. Use `"*"` to allow all origins, or restrict to e.g. `"https://yoursite.com"`. |
| `AllowDropFromWeb` | bool | `true` | Whether web/API clients may submit drop requests. |
| `AllowOrderFromWeb` | bool | `true` | Whether web/API clients may submit order requests. |
| `ApiKey` | string | `""` | Optional secret key. If set, all requests must include it. Leave empty to disable. |
| `AllowLocalhostOnly` | bool | `false` | If `true`, rejects all requests from non-localhost IPs. |

### Step 4 — Start the Bot

Launch `SysBot.ACNHOrders.exe` normally. You will see in the console:

```
[HttpAPI] HTTP REST API server listening on http://localhost:5202/ and http://127.0.0.1:5202/
[SocketAPI] Socket API server listening on port 5201.
```

> **Both servers start before the Switch connects.** Status endpoints will return `is_running: true` once the bot object is initialized, even if ACNH hasn't loaded yet.

### Step 5 — Verify

Open a browser and go to `http://localhost:5202/api` — you should see:

```json
{
  "service": "SysBot.ACNHOrders API",
  "version": "2.0",
  "status": "running",
  "endpoints": ["GET  /api/status", "GET  /api/dodo", ...]
}
```

---

## 2. Windows-Specific Notes

### HTTP API — No Admin Required (localhost only)

By default, the HTTP API binds to `http://localhost:5202/` and `http://127.0.0.1:5202/`. **These work without administrator rights.**

### Accessing the API from Other Devices on Your Network

If you want other devices (your website server, other PCs) to reach the bot directly by IP, you need the wildcard prefix `http://*:5202/`. This **requires both**:

1. Run the following **once as Administrator** in a Command Prompt:
   ```
   netsh http add urlacl url=http://*:5202/ user=Everyone
   ```
2. Set `"HttpPrefix": "http://*:5202/"` in `server.json`.

**Alternative (recommended):** Put a reverse proxy (nginx, Caddy) in front of `localhost:5202` instead of running the bot as admin.

### Firewall

If accessing from another machine, allow the port through Windows Firewall:
```
netsh advfirewall firewall add rule name="SysBot HTTP API" dir=in action=allow protocol=TCP localport=5202
netsh advfirewall firewall add rule name="SysBot Socket API" dir=in action=allow protocol=TCP localport=5201
```

---

## 3. Authentication (Optional)

If you set `ApiKey` in `server.json`, **every HTTP request** must include the key via either header:

```
X-API-Key: your_secret_key
```
or
```
Authorization: Bearer your_secret_key
```

Without a valid key, the server returns `401 Unauthorized`:
```json
{ "success": false, "error": "Unauthorized: Invalid or missing API key." }
```

Leave `ApiKey` as `""` (empty) to disable authentication entirely.

---

## 4. HTTP REST API Reference

**Base URL:** `http://localhost:5202` (replace with your bot machine's IP if remote)

All responses are JSON. All POST endpoints accept `Content-Type: application/json`.  
CORS headers are included on every response — web frontends can call the API directly.

---

### `GET /api/status`

Returns the full bot status: mode, island name, Dodo code (if in Drop Mode), visitor list, queue count, battery, and whether commands are being accepted.

**Response:**
```json
{
  "success": true,
  "is_running": true,
  "mode": "DropMode",
  "is_drop_mode": true,
  "is_order_mode": false,
  "island_name": "Sinta",
  "dodo_code": "CH0P1",
  "layer": "TreasureLayer1",
  "visitors_count": 3,
  "visitors": "Player1\nPlayer2\nPlayer3",
  "visitor_list": ["Player1", "Player2", "Player3"],
  "is_dirty": false,
  "accepting_commands": true,
  "queue_count": 0,
  "battery_charge": 85,
  "last_dodo_fetch": "2026-08-24 12:30:00",
  "server_time": "2026-08-24T04:30:00.000Z"
}
```

> **Tip:** Check `is_drop_mode` vs `is_order_mode` to decide which UI to show your users. Check `accepting_commands` before allowing submissions.

---

### `GET /api/dodo`

Returns the Dodo code. Behavior depends on the bot's current mode:

- **Drop Mode** → Returns the live Dodo code immediately.
- **Order Mode (no params)** → Returns `null`. Users must submit an order first.
- **Order Mode with `?order_id=...`** → Returns the Dodo code once the order is ready, plus queue position and ETA while waiting.

**Query Parameters:**

| Parameter | Description |
|---|---|
| `order_id` | The order ID returned from `POST /api/order` |
| `user_id` | Numeric user GUID (alternative to `order_id`) |

**Response — Drop Mode:**
```json
{
  "success": true,
  "mode": "DropMode",
  "dodo_code": "CH0P1",
  "is_valid": true,
  "island_name": "Sinta",
  "layer": "TreasureLayer1",
  "visitors_count": 2,
  "message": "Dodo code for Sinta is CH0P1"
}
```

**Response — Order Mode, order queued (Dodo not yet available):**
```json
{
  "success": true,
  "mode": "OrderMode",
  "order_id": "web_a1b2c3d4",
  "user_name": "ChoPaeng",
  "status": "queued",
  "queue_position": 2,
  "eta": "04m:00s",
  "dodo_code": null,
  "island_name": "Sinta",
  "message": "In queue at position 2."
}
```

**Response — Order Mode, order ready:**
```json
{
  "success": true,
  "mode": "OrderMode",
  "order_id": "web_a1b2c3d4",
  "status": "ready",
  "queue_position": 0,
  "eta": "00m:00s",
  "dodo_code": "CH0P1",
  "island_name": "Sinta",
  "message": "Your order is ready! Dodo code is: CH0P1"
}
```

---

### `GET /api/queue`

Returns the full list of orders currently in queue.

**Response:**
```json
{
  "success": true,
  "count": 2,
  "island_name": "Sinta",
  "current_active_user": "ChoPaeng",
  "orders": [
    {
      "position": 1,
      "order_id": "web_a1b2c3d4",
      "user_guid": "1234567890",
      "username": "ChoPaeng",
      "villager": "Raymond",
      "item_count": 40,
      "status": "next",
      "eta": "00m:00s",
      "created_at": "2026-08-24T04:25:00.000Z"
    },
    {
      "position": 2,
      "order_id": "web_e5f6g7h8",
      "username": "Player2",
      "item_count": 20,
      "status": "queued",
      "eta": "02m:00s"
    }
  ]
}
```

---

### `GET /api/order/status?id={order_id}`

Polls the real-time status of a specific order. Use this to track queue position and detect when the Dodo code is available.

**Query Parameters:**

| Parameter | Description |
|---|---|
| `id` or `order_id` | Order ID from `POST /api/order` |
| `user_id` | Numeric user GUID (alternative) |

**Response:**
```json
{
  "success": true,
  "found": true,
  "order_id": "web_a1b2c3d4",
  "user_guid": "1234567890",
  "username": "ChoPaeng",
  "villager_name": "Raymond",
  "item_count": 40,
  "status": "queued",
  "queue_position": 1,
  "eta": "02m:00s",
  "estimated_seconds": 120,
  "dodo_code": null,
  "island_name": "Sinta",
  "message": "In queue at position 1.",
  "created_at": "2026-08-24T04:25:00.000Z",
  "updated_at": "2026-08-24T04:26:00.000Z"
}
```

> **Polling tip:** Poll every 10–15 seconds. When `status` becomes `"ready"` and `dodo_code` is non-null, show the Dodo code to the user.

**Order status values:**

| `status` | Meaning |
|---|---|
| `queued` | In queue, waiting |
| `next` | First in queue, being prepared |
| `ready` | Dodo code is available |
| `not_found` | Order ID not found / already expired |

---

### `POST /api/order`

Submits an order to the queue. Only works in **Order Mode** (`is_order_mode: true`).

**Request formats:**

*By item name/hex (comma-separated):*
```json
{
  "order": "Royal crown 40, Gold nugget 30",
  "username": "ChoPaeng",
  "order_id": "optional_custom_id"
}
```

*With a villager adoption:*
```json
{
  "order": "Gold nugget 30",
  "villager": "Raymond",
  "username": "ChoPaeng"
}
```

*By preset name:*
```json
{
  "preset": "materials",
  "username": "ChoPaeng"
}
```

*Array format:*
```json
{
  "items": ["Royal crown 40", "Gold nugget 30"],
  "username": "ChoPaeng"
}
```

**Request fields:**

| Field | Required | Description |
|---|---|---|
| `order` | * | Comma-separated item names or hex IDs (e.g. `0x1234`) |
| `items` | * | Array of item strings — alternative to `order` |
| `preset` | * | Name of a saved preset — alternative to `order` |
| `villager` | No | Villager name to adopt (e.g. `"Raymond"`) |
| `username` | No | Display name shown in queue (default: `"WebUser"`) |
| `order_id` | No | Custom ID string. Auto-generated as `web_xxxxxxxx` if omitted. |
| `user_id` | No | Numeric user GUID for deduplication |

*\* Provide exactly one of `order`, `items`, or `preset`.*

**Response:**
```json
{
  "success": true,
  "order_id": "web_a1b2c3d4",
  "user_guid": "1234567890",
  "queue_position": 2,
  "eta": "04m:00s",
  "estimated_seconds": 240,
  "status": "queued",
  "island_name": "Sinta",
  "item_count": 40,
  "villager": "Raymond",
  "message": "Successfully added to queue at position 2. Predicted ETA: 04m:00s."
}
```

> Save the returned `order_id` — you'll need it to poll `/api/order/status` and get the Dodo code.

---

### `POST /api/order/cancel`

Cancels and removes an order from the queue.

**Request:**
```json
{ "id": "web_a1b2c3d4" }
```

Also accepts `DELETE /api/order?id=web_a1b2c3d4`.

**Response:**
```json
{
  "success": true,
  "order_id": "web_a1b2c3d4",
  "message": "Order successfully cancelled and removed from queue."
}
```

---

### `POST /api/drop`

Queues an item drop at the bot's location. Works in both modes (as long as `AllowDrop` is enabled in `config.json`).

**Request:**
```json
{
  "items": "Gold nugget 30, Iron nugget 10",
  "type": "items",
  "username": "WebUser"
}
```

*For DIY recipes:*
```json
{
  "items": "Ironwood kitchenette, Golden axe",
  "type": "diy",
  "username": "WebUser"
}
```

**Request fields:**

| Field | Required | Description |
|---|---|---|
| `items` | Yes | Item names or hex IDs, comma-separated (or array) |
| `type` | No | `"items"` (default) or `"diy"` for recipes |
| `username` | No | Display name for logging |
| `count` | No | Override the max drop count |

**Response:**
```json
{
  "success": true,
  "dropped_count": 40,
  "island_name": "Sinta",
  "message": "40 item drop request(s) queued and will execute momentarily."
}
```

---

### `POST /api/clean`

Triggers the bot to pick up (clean) all dropped items on the ground.

**Request:** *(no body needed)*

**Response:**
```json
{
  "success": true,
  "message": "Clean request queued and will be executed momentarily."
}
```

> Requires `AllowClean: true` in `config.json`.

---

### `POST /api/turnips`

Sets the island's turnip (stalk market) price.

**Request:**
```json
{ "value": 999999999 }
```

Also accepts `GET /api/turnips/max` to set the maximum price directly.

**Response:**
```json
{
  "success": true,
  "price": 999999999,
  "message": "Turnip stonk value queued to be set to 999999999."
}
```

---

### `POST /api/speak`

Sends a message to the in-game Switch chat.

**Request:**
```json
{ "message": "Welcome to Sinta! Dodo code is CH0P1." }
```

**Response:**
```json
{
  "success": true,
  "message": "Speak message queued: 'Welcome to Sinta! Dodo code is CH0P1.'"
}
```

---

### `GET /api/presets`

Lists all available preset names configured in the bot.

**Response:**
```json
{
  "success": true,
  "count": 3,
  "presets": ["materials", "furniture", "bells"]
}
```

---

### `GET /api/villagers`

Returns the list of villagers currently on the island.

**Response:**
```json
{
  "success": true,
  "count": 5,
  "island_name": "Sinta",
  "villagers": ["Raymond", "Marshal", "Fauna", "Merengue", "Tangy"]
}
```

---

## 5. TCP Socket API Reference

The Socket API on port `5201` uses a simple newline-delimited JSON protocol. All messages are **UTF-8 encoded** and terminated with `\0\0` (two null bytes).

> Enable it by setting `"Enabled": true` in `server.json`.

### Request Format

```json
{
  "id": "any_string_you_choose",
  "endpoint": "GetStatus",
  "args": ""
}
```

| Field | Description |
|---|---|
| `id` | Your custom ID echoed back in the response for correlation |
| `endpoint` | Name of the endpoint to call (case-sensitive) |
| `args` | JSON string of arguments (endpoint-specific, can be `""` or `null`) |

### Response Format

```json
{
  "id": "any_string_you_chose",
  "type": "Response",
  "value": { ... }
}
```

On error:
```json
{
  "id": "any_string_you_chose",
  "type": "Response",
  "error": "The supplied endpoint was not found."
}
```

---

### Available Endpoints

| Endpoint | Args | Description |
|---|---|---|
| `GetStatus` | *(none)* | Full bot status (same as `GET /api/status`) |
| `GetDodo` | `{"order_id":"..."}` or `{"user_id":"..."}` | Dodo code (same as `GET /api/dodo`) |
| `GetQueue` | *(none)* | Current queue list |
| `GetOrderStatus` | `{"id":"web_a1b2c3d4"}` | Status of a specific order |
| `RequestOrder` | See below | Submit an order |
| `CancelOrder` | `{"id":"web_a1b2c3d4"}` | Cancel an order |
| `RequestDrop` | See below | Submit a drop |
| `RequestClean` | *(none)* | Trigger item cleanup |
| `SetTurnips` | `{"value":999999999}` | Set turnip price |
| `Speak` | `{"message":"Hello!"}` | In-game chat |
| `ListPresets` | *(none)* | List preset names |
| `ListVillagers` | *(none)* | List island villagers |

**`RequestOrder` args:**
```json
{
  "order": "Gold nugget 30",
  "villager": "Raymond",
  "username": "Player1",
  "order_id": "my_order_001"
}
```

**`RequestDrop` args:**
```json
{
  "items": "Gold nugget 30, Iron nugget 10",
  "type": "items",
  "username": "Player1"
}
```

### Python Socket Client Example

```python
import socket
import json

def send_socket_request(endpoint, args=""):
    req = json.dumps({"id": "1", "endpoint": endpoint, "args": args}) + "\n"
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect(("localhost", 5201))
        s.sendall(req.encode("utf-8"))
        data = b""
        while True:
            chunk = s.recv(4096)
            data += chunk
            if b"\x00\x00" in data:
                break
    response = data.decode("utf-8").rstrip("\x00")
    return json.loads(response)

# Get status
status = send_socket_request("GetStatus")
print("Mode:", status["value"]["mode"])

# Submit order
result = send_socket_request("RequestOrder", json.dumps({
    "order": "Gold nugget 30",
    "username": "Player1"
}))
print("Order ID:", result["value"]["order_id"])
```

---

## 6. Mode-Aware Behavior

The bot operates in one of two exclusive modes, controlled by `DodoModeConfig.LimitedDodoRestoreOnlyMode` in `config.json`.

| Behavior | Drop Mode | Order Mode |
|---|---|---|
| `/api/status` → `mode` | `"DropMode"` | `"OrderMode"` |
| `/api/status` → `is_drop_mode` | `true` | `false` |
| `/api/dodo` → `dodo_code` | Live code returned | `null` (until order ready) |
| `POST /api/order` | ❌ Returns error | ✅ Works |
| `POST /api/drop` | ✅ Works | ✅ Works (if `AllowDrop: true`) |
| Dodo code visibility | Always public via `/api/dodo` | Per-order via `/api/dodo?order_id=...` |

**Frontend pattern:** Always call `GET /api/status` first on page load, then use `is_drop_mode` / `is_order_mode` to conditionally render the correct UI.

---

## 7. Integration Examples

### JavaScript / Web Frontend

```javascript
const API = "http://localhost:5202";  // Replace with your bot's URL

// Check mode and accepting status
async function getBotStatus() {
  const res = await fetch(`${API}/api/status`);
  return await res.json();
}

// Submit an order, returns order_id
async function submitOrder(itemsText, username) {
  const res = await fetch(`${API}/api/order`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ order: itemsText, username })
  });
  return await res.json();
}

// Poll until dodo_code is available, then resolve
async function waitForDodo(orderId, intervalMs = 10000) {
  return new Promise((resolve) => {
    const timer = setInterval(async () => {
      const res = await fetch(`${API}/api/order/status?id=${encodeURIComponent(orderId)}`);
      const data = await res.json();
      if (data.status === "ready" && data.dodo_code) {
        clearInterval(timer);
        resolve(data.dodo_code);
      }
      console.log(`Position: ${data.queue_position}, ETA: ${data.eta}`);
    }, intervalMs);
  });
}

// Get Dodo code in Drop Mode
async function getDropDodo() {
  const res = await fetch(`${API}/api/dodo`);
  return (await res.json()).dodo_code;
}

// Full order flow example
async function placeOrderFlow(items, username) {
  const status = await getBotStatus();
  if (!status.is_order_mode) return console.error("Bot is not in Order Mode.");
  if (!status.accepting_commands) return console.error("Bot is not accepting commands.");

  const order = await submitOrder(items, username);
  if (!order.success) return console.error("Failed:", order.message);

  console.log(`Queued at position ${order.queue_position}, ETA: ${order.eta}`);
  const dodo = await waitForDodo(order.order_id);
  console.log("Your Dodo Code:", dodo);
}
```

### Python / Backend

```python
import requests
import time

BASE = "http://localhost:5202"

def get_status():
    return requests.get(f"{BASE}/api/status").json()

def submit_order(order_text, username="WebUser", villager=None):
    payload = {"order": order_text, "username": username}
    if villager:
        payload["villager"] = villager
    return requests.post(f"{BASE}/api/order", json=payload).json()

def wait_for_dodo(order_id, poll_interval=10):
    while True:
        data = requests.get(f"{BASE}/api/order/status", params={"id": order_id}).json()
        print(f"  Position: {data.get('queue_position')}, ETA: {data.get('eta')}, Status: {data.get('status')}")
        if data.get("status") == "ready" and data.get("dodo_code"):
            return data["dodo_code"]
        time.sleep(poll_interval)

# --- Full Order Flow ---
status = get_status()
print(f"Mode: {status['mode']} | Accepting: {status['accepting_commands']}")

if status["is_order_mode"] and status["accepting_commands"]:
    order = submit_order("Gold nugget 30", username="Player1", villager="Raymond")
    print(f"Queued! ID: {order['order_id']}, Position: {order['queue_position']}")

    dodo = wait_for_dodo(order["order_id"])
    print(f"Dodo Code: {dodo}")

# --- Drop Mode ---
if status["is_drop_mode"]:
    dodo = requests.get(f"{BASE}/api/dodo").json().get("dodo_code")
    print(f"Drop Island Dodo: {dodo}")
```

---

## 8. Error Reference

### HTTP Status Codes

| Code | Meaning |
|---|---|
| `200 OK` | Request succeeded |
| `204 No Content` | CORS preflight OPTIONS response |
| `401 Unauthorized` | Missing or invalid API key |
| `403 Forbidden` | Request from non-localhost when `AllowLocalhostOnly: true` |
| `404 Not Found` | Endpoint path does not exist |
| `405 Method Not Allowed` | Wrong HTTP method for endpoint |
| `500 Internal Server Error` | Unexpected bot-side error (check bot console) |

### Common `success: false` Messages

| Message | Cause |
|---|---|
| `"Bot is not running."` | `Globals.Bot` is null — bot hasn't initialized yet |
| `"Bot is not currently accepting order commands."` | `AcceptingCommands: false` in `config.json` |
| `"Bot is currently running in Drop/Dodo Restore mode. Orders are disabled."` | Called `/api/order` while in Drop Mode |
| `"AllowDrop is currently disabled in configuration."` | `AllowDrop: false` in `config.json` |
| `"Villager injection is currently disabled on this bot."` | `AllowVillagerInjection: false` in `config.json` |
| `"User is already in queue at position N."` | Same user GUID already queued |
| `"Order not found in queue or has already expired."` | Order ID unknown or already completed |
| `"Order contains unsafe items..."` | Item would corrupt save data — rejected for safety |
| `"Failed to convert item (index N: ...) for Language en."` | Item name not recognized or GameInfo not loaded |
