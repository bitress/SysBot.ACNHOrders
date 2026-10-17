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
  "endpoints": ["GET  /api/status", "GET  /api/gametime", "POST /api/togglerequests", "POST /api/togglemashb", "POST /api/togglerefresh", "GET  /api/dodo", ...]
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

Returns the full bot status: mode, island name, Dodo code (if in Drop Mode), Switch connection state, visitor list, arrivals/departures, recent activity events, queue count, battery, and whether commands are being accepted.

**Response:**
```json
{
  "success": true,
  "is_running": true,
  "is_connected": true,
  "console_stage": "Running",
  "mode": "DropMode",
  "is_drop_mode": true,
  "is_order_mode": false,
  "island_name": "Sinta",
  "dodo_code": "CH0P1",
  "is_dodo_valid": true,
  "is_refreshing_dodo": false,
  "layer": "TreasureLayer1",
  "visitors_count": 3,
  "visitors": "Player1\nPlayer2\nPlayer3",
  "visitor_list": ["Player1", "Player2", "Player3"],
  "last_arrival": {
    "player": "Player3",
    "island": "Paradise",
    "town": "Sinta",
    "timestamp": "2026-08-24T04:28:10.000Z",
    "localTime": "2026-08-24 12:28:10 PM"
  },
  "last_departure": {
    "player": "Player0",
    "town": "Sinta",
    "timestamp": "2026-08-24T04:20:00.000Z",
    "localTime": "2026-08-24 12:20:00 PM"
  },
  "recent_events": [
    {
      "id": 15,
      "timestamp": "2026-08-24T04:28:10.000Z",
      "localTime": "2026-08-24 12:28:10 PM",
      "type": "arrival",
      "message": "🛬 Player3 from Paradise is joining Sinta. Dodo code is: CH0P1.",
      "player": "Player3",
      "island": "Paradise",
      "town": "Sinta"
    }
  ],
  "is_dirty": false,
  "accepting_commands": true,
  "mash_b": false,
  "refresh_map": true,
  "queue_count": 0,
  "battery_charge": 85,
  "last_dodo_fetch": "2026-08-24 12:30:00",
  "game_time": "14:30 (2026-10-13)",
  "is_nooks_open": true,
  "server_time": "2026-08-24T04:30:00.000Z"
}
```

> **Tip:** Check `is_drop_mode` vs `is_order_mode` to decide which UI to show your users. Check `is_dodo_valid` and `is_refreshing_dodo` to show loading spinners while a new Dodo code is being fetched.

---

### `GET /api/gametime`

Returns the current in-game clock time read from the Switch console memory, formatted time string, date components, and Nook's Cranny store open/closed status. Equivalent to Discord `!gameTime` / `/gametime`.

**Aliases:** `GET /gametime`, `GET /api/time`, `GET /time`

**Response:**
```json
{
  "success": true,
  "game_time": "14:30 (2026-10-13)",
  "formatted": "14:30 (2026-10-13)",
  "year": 2026,
  "month": 10,
  "day": 13,
  "hour": 14,
  "minute": 30,
  "is_nooks_open": true,
  "nooks_status": "open",
  "nooks_message": "Nook's Cranny is expected to be open.",
  "mode": "DropMode",
  "message": "The current in-game time is: 14:30 (2026-10-13) (Nook's Cranny is expected to be open.)"
}
```

---

### `GET /api/events`

Returns a chronological ring buffer (up to 200 items) of recent island activity: visitor arrivals, departures, Dodo refreshes, crashes, item layer switches, and system echoes — working completely standalone without Discord.

**Aliases:** `GET /api/activity`, `GET /events`

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `limit` | number | `50` | Maximum number of events to return (1–200) |
| `since_id` / `since` | number | `null` | Return only events strictly newer than this event ID (incremental polling) |
| `type` | string | `null` | Filter by event type: `arrival`, `departure`, `dodo`, `crash`, `layer`, `echo`, `system` |

**Response:**
```json
{
  "success": true,
  "events": [
    {
      "id": 15,
      "timestamp": "2026-08-24T04:28:10.000Z",
      "localTime": "2026-08-24 12:28:10 PM",
      "type": "arrival",
      "message": "🛬 Player3 from Paradise is joining Sinta. Dodo code is: CH0P1.",
      "player": "Player3",
      "island": "Paradise",
      "town": "Sinta",
      "dodoCode": "CH0P1"
    },
    {
      "id": 14,
      "timestamp": "2026-08-24T04:20:00.000Z",
      "localTime": "2026-08-24 12:20:00 PM",
      "type": "departure",
      "message": "🛫 Player0 has departed from Sinta",
      "player": "Player0",
      "town": "Sinta"
    }
  ],
  "count": 2,
  "total_recorded": 15,
  "last_arrival": { ... },
  "last_departure": { ... },
  "server_time": "2026-08-24T04:30:00.000Z"
}
```

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

### `POST /api/villager` & `POST /api/villagers`

Injects one or multiple villagers into island house plots (plots 1–10).

**Single injection (`POST /api/villager`):**
```json
{
  "villager": "Raymond",
  "plot": 1
}
```

**Batch injection (`POST /api/villagers`):**
```json
{
  "villagers": [
    { "villager": "Raymond", "plot": 1 },
    { "villager": "Marshal", "plot": 2 },
    { "villager": "Judy", "plot": 3 }
  ]
}
```

*Or as a list of strings (plots assigned automatically):*
```json
["Raymond", "Marshal", "Judy", "Shino", "Sasha"]
```

**Response:**
```json
{
  "success": true,
  "injected_count": 3,
  "injected": [
    { "villager": "Raymond", "internal_name": "cat23", "plot": 1, "house_index": 0 },
    { "villager": "Marshal", "internal_name": "squ17", "plot": 2, "house_index": 1 },
    { "villager": "Judy", "internal_name": "cbr18", "plot": 3, "house_index": 2 }
  ],
  "queue_length": 3,
  "message": "Successfully queued 3 villager(s) for injection into plots."
}
```

---

### `POST /api/dodo/refresh`

Triggers SysBot to restart the Switch game loop and fetch a fresh Dodo code.

**Aliases:** `POST /api/newdodo`, `POST /api/dodo/restart`, `POST /api/restart`

**Request:**
```json
{
  "delay_seconds": 0
}
```

**Response:**
```json
{
  "success": true,
  "island_name": "Sinta",
  "message": "Dodo refresh requested. Bot will restart the game and fetch a new Dodo code."
}
```

---

### `POST /api/click`

Sends virtual controller button clicks to the Switch. Useful for unsticking airport dialogues or dismissing menus without physical controller access.

**Aliases:** `POST /api/button`, `POST /api/unstick`

**Request — Unstick (mashes B 5 times):**
```json
{
  "button": "UNSTICK"
}
```

**Request — Specific Button Press:**
```json
{
  "button": "B",
  "count": 3,
  "delay_ms": 200
}
```

*Valid buttons:* `A`, `B`, `X`, `Y`, `L`, `R`, `ZL`, `ZR`, `PLUS`, `MINUS`, `DUP`, `DDOWN`, `DLEFT`, `DRIGHT`, `HOME`, `CAPTURE`, `UNSTICK`, `DETACH`.

**Response:**
```json
{
  "success": true,
  "button": "B",
  "count": 3,
  "delay_ms": 200,
  "message": "Successfully clicked B 3 time(s)."
}
```

---

### `GET /api/items/search`

Searches the in-game ACNH item database by name or keyword with fuzzy matching and returns matching item names, integer IDs, and 4-digit hex codes.

**Alias:** `GET /api/items`

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `query` / `q` | string | *(required)* | Search query (min 2 characters, e.g. `gold`) |
| `limit` | number | `20` | Max items to return (1–100) |
| `language` / `lang` | string | `"en"` | Language code (`en`, `jp`, `fr`, `de`, `es`, `it`, `ko`, `chs`, `cht`) |

**Response:**
```json
{
  "success": true,
  "query": "gold",
  "language": "en",
  "count": 3,
  "items": [
    { "name": "Gold nugget", "id": 2505, "hex": "09C9" },
    { "name": "Golden axe", "id": 9617, "hex": "2591" },
    { "name": "Golden shovel", "id": 8574, "hex": "217E" }
  ]
}
```

---

### `POST /api/togglerequests`

Toggles or explicitly sets whether the bot accepts incoming drop and order requests (`AcceptingCommands`). Equivalent to Discord `!toggleRequests`.

**Aliases:** `POST /togglerequests`, `POST /api/requests/toggle`

**Parameters (Optional):**
* Query string: `?enabled=true` or `?value=false`
* JSON body: `{"enabled": true}` or `{"value": false}`
* If no parameter is provided, it flips the current state.

**Response:**
```json
{
  "success": true,
  "accepting_commands": false,
  "message": "Accepting requests: False."
}
```

---

### `POST /api/togglemashb`

Toggles or explicitly sets whether the bot mashes the B button during dialogue loops (Treasure Island / Dodo restore mode only). Equivalent to Discord `!toggleMashB`.

**Aliases:** `POST /togglemashb`, `POST /api/mashb/toggle`

**Parameters (Optional):**
* Query string: `?enabled=true` or `?value=false`
* JSON body: `{"enabled": true}` or `{"value": false}`
* If no parameter is provided, it flips the current state.

**Response:**
```json
{
  "success": true,
  "mash_b": true,
  "message": "Mash B set to: True."
}
```

---

### `POST /api/togglerefresh`

Toggles or explicitly sets whether the bot refreshes the map (Treasure Island / Dodo restore mode only). Equivalent to Discord `!toggleRefresh`.

**Aliases:** `POST /togglerefresh`, `POST /api/refresh/toggle`, `POST /api/refreshmap`, `POST /refreshmap`

**Parameters (Optional):**
* Query string: `?enabled=true` or `?value=false`
* JSON body: `{"enabled": true}` or `{"value": false}`
* If no parameter is provided, it flips the current state.

**Response:**
```json
{
  "success": true,
  "refresh_map": true,
  "message": "RefreshMap set to: True."
}
```

---

### `POST /api/control/start`

Starts the console loop or reconnects it after an error, and opens requests (`AcceptingCommands = true`). It does not launch a stopped server process. Equivalent to Discord Sudo Control Panel **Start bot**.

**Aliases:** `POST /api/bot/start`, `POST /startbot`

**Response:**
```json
{
  "success": true,
  "stage": "Starting",
  "accepting_commands": true,
  "message": "The bot is starting or already running."
}
```

---

### `POST /api/control/stop`

Pauses requests, waits for the current order to finish, then stops the console loop and disconnects the Switch. Discord and waiting orders stay available. Equivalent to Discord Sudo Control Panel **Stop bot**.

**Aliases:** `POST /api/bot/stop`, `POST /stopbot`

**Response:**
```json
{
  "success": true,
  "stage": "Stopping",
  "message": "The bot will stop after the current order finishes. Discord and the waiting queue stay available."
}
```

---

### `POST /api/control/restart`

Pauses requests, waits for the current order to finish, then stops and starts the console loop. Requests stay paused until you call Resume requests. Equivalent to Discord Sudo Control Panel **Restart connection**.

**Aliases:** `POST /api/bot/restart`, `POST /restartconnection`

**Response:**
```json
{
  "success": true,
  "stage": "Stopping",
  "message": "The console connection will restart after the current order finishes. Requests are paused, use Resume requests when ready."
}
```

---

### `POST /api/requests/pause`

Pauses new drop and order requests and queue processing. The currently executing order is allowed to finish. Equivalent to Discord Sudo Control Panel **Pause requests**.

**Aliases:** `POST /api/control/pause`, `POST /api/pause`, `POST /pause`

**Response:**
```json
{
  "success": true,
  "accepting_commands": false,
  "message": "Requests are paused. The current order can finish."
}
```

---

### `POST /api/requests/resume`

Opens requests (`AcceptingCommands = true`) while the console loop is starting or running. Equivalent to Discord Sudo Control Panel **Resume requests**.

**Aliases:** `POST /api/control/resume`, `POST /api/resume`, `POST /resume`

**Response:**
```json
{
  "success": true,
  "accepting_commands": true,
  "message": "Requests are open."
}
```

---

### `POST /api/screen`

Sends the screen power command to the connected Switch. Equivalent to Discord Sudo Control Panel **Screen on / Screen off** or `!setScreenOn` / `!setScreenOff`.

**Aliases:** `POST /screen`, `POST /api/setscreen`  
**Direct Endpoints:** `POST /api/screen/on`, `POST /api/screen/off`

**Parameters (Optional):**
* Query string: `?on=true` or `?on=false`
* JSON body: `{"on": true}` or `{"on": false}` (default is `true` / on)

**Response:**
```json
{
  "success": true,
  "screen_on": false,
  "message": "Screen off command sent."
}
```

---

### `POST /api/detach`

Detaches the virtual controller from the Switch so an operator can use their physical handheld controller temporarily. The bot will automatically reattach when its next game action executes. Equivalent to Discord Sudo Control Panel **Detach controller** or `!detach`.

**Aliases:** `POST /detach`, `POST /api/controller/detach`

**Response:**
```json
{
  "success": true,
  "message": "Controller detach command sent."
}
```

---

### `GET /api/control/guide`

Returns a structured list of all 12 console controls and their explanations. Equivalent to Discord Sudo Control Panel **Guide**.

**Aliases:** `GET /api/guide`, `GET /guide`

**Response:**
```json
{
  "success": true,
  "guide": [
    { "control": "Start bot", "description": "Starts the console loop or reconnects it after an error, and opens requests. It does not launch a stopped server process." },
    { "control": "Pause requests", "description": "Pauses new requests and queue processing. The current order can finish." },
    { "control": "Resume requests", "description": "Opens requests while the console loop is starting or running." },
    { "control": "Stop bot", "description": "Pauses requests, waits for the current order to finish, then stops the console loop and disconnects the Switch. Discord and waiting orders stay available." },
    { "control": "Restart connection", "description": "Pauses requests, waits for the current order to finish, then stops and starts the console loop. Requests stay paused until you press Resume requests." },
    { "control": "Screen on / Screen off", "description": "Sends the corresponding screen command to the connected Switch." },
    { "control": "Detach controller", "description": "Detaches the virtual controller. The bot may attach it again when its next action runs." },
    { "control": "View queue", "description": "Shows waiting names in a private reply." },
    { "control": "New Dodo code", "description": "Requests a game restart and a new code in restore mode. Visitors will be disconnected." },
    { "control": "Mash B on / off", "description": "Changes automatic dialogue button presses in restore mode." },
    { "control": "Map refresh on / off", "description": "Changes automatic map refresh in restore mode." },
    { "control": "Guide", "description": "Opens a short explanation of the controls." }
  ]
}
```

---

### `POST /api/control` (Unified Dispatch)

Dispatches any control action by name in a single endpoint. Matches the exact Discord control panel action identifiers.

**Body (JSON):**
```json
{
  "action": "start"
}
```

**Supported Actions:**  
`start`, `stop`, `restart`, `pause`, `resume`, `screen-on`, `screen-off`, `detach`, `newdodo`, `mash-on`, `mash-off`, `map-on`, `map-off`, `queue`, `guide`.

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
| `GetGameTime` | *(none)* | In-game clock time & Nook's Cranny status (same as `GET /api/gametime`) |
| `StartBot` | *(none)* | Starts console loop and opens requests |
| `StopBot` | *(none)* | Stops console loop after current order |
| `RestartConnection` | *(none)* | Restarts console connection after current order |
| `PauseRequests` | *(none)* | Pauses drop and order requests |
| `ResumeRequests` | *(none)* | Resumes drop and order requests |
| `SetScreen` | `{"on":true}` or `{"on":false}` | Controls Switch screen power |
| `DetachController` | *(none)* | Detaches virtual controller |
| `GetControlGuide` | *(none)* | Returns 12 console controls guide |
| `Control` | `{"action":"start"}` | Unified control action dispatcher |
| `ToggleRequests` | `{"enabled":true}` or *(none)* | Toggle or set accepting drop/order requests |
| `ToggleMashB` | `{"enabled":true}` or *(none)* | Toggle or set dialogue B button mashing |
| `ToggleRefresh` | `{"enabled":true}` or *(none)* | Toggle or set automatic map refreshing |
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
| `GetEvents` | `{"limit":50,"since_id":10,"type":"arrival"}` | Chronological activity log |
| `RequestDodoRefresh` / `NewDodo` | `{"delay_seconds":0}` | Trigger game restart and fetch fresh Dodo code (equivalent to `/newdodo`) |
| `Click` | `{"button":"B","count":3,"delay_ms":200}` | Virtual controller button press or `UNSTICK` |
| `SearchItems` | `{"query":"gold","limit":20,"language":"en"}` | Search in-game item database |

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
| `"Bot Switch connection is offline or unavailable."` | Switch is not connected via USB/LAN socket to SysBot |
| `"Unknown button '...' Valid buttons: ..."` | Button name passed to `/api/click` is unrecognized |
| `"Query parameter must be at least 2 characters."` | Item search query passed to `/api/items/search` is too short |
| `"No villagers could be queued. See errors."` | Invalid villager name or invalid plot index passed to `/api/villagers` |
