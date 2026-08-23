# SysBot.ACNHOrders Web & Socket API Server

SysBot.ACNHOrders includes two high-performance API interfaces:
1. **HTTP REST API Server** (Port `5202` by default): For web browsers, websites (e.g. Next.js / React / Vue / HTML), Python / Flask backends, and curl. Built-in CORS (`Access-Control-Allow-Origin: *`) enables direct calls from any web frontend.
2. **TCP Socket API Server** (Port `5201` by default): For direct TCP JSON socket clients and bot controllers.

---

## Configuration (`server.json`)

The server configuration is located in `server.json` alongside the executable:

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
  "AllowOrderFromWeb": true
}
```

---

## HTTP REST API Endpoints

Base URL: `http://localhost:5202` (or your bot's IP/domain)

### 1. General & Status

#### `GET /api/status`
Returns complete status of the bot, island name, mode (`DropMode` vs `OrderMode`), visitor list, and queue length.

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
  "visitor_list": ["Player1", "Player2", "Player3"],
  "is_dirty": false,
  "accepting_commands": true,
  "queue_count": 0,
  "battery_charge": 100,
  "last_dodo_fetch": "2026-08-24 00:30:00"
}
```

---

### 2. Dodo Codes

#### `GET /api/dodo`
- **In Drop Mode / Treasure Island Mode**: Returns the live Dodo code for the island.
- **In Order Mode**: If an `order_id` or `user_id` query parameter is passed (e.g. `/api/dodo?order_id=web_a1b2c3d4`), returns the order's status and the Dodo code as soon as the bot has prepared the order.

**Query Parameters:**
- `order_id` (optional): The ID of the order
- `user_id` (optional): User numeric identifier

**Response (Drop Mode):**
```json
{
  "success": true,
  "mode": "DropMode",
  "dodo_code": "CH0P1",
  "is_valid": true,
  "island_name": "Sinta",
  "visitors_count": 2,
  "message": "Dodo code for Sinta is CH0P1"
}
```

**Response (Order Mode - Ready):**
```json
{
  "success": true,
  "mode": "OrderMode",
  "order_id": "web_a1b2c3d4",
  "user_name": "ChoPaeng",
  "status": "ready",
  "queue_position": 0,
  "eta": "00m:00s",
  "dodo_code": "CH0P1",
  "island_name": "Sinta",
  "message": "Your order is ready! Dodo code is: CH0P1"
}
```

---

### 3. Order Mode & Queue Management

#### `POST /api/order`
Submits an item order, villager order, or preset order to the queue.

**Request Body (JSON):**
```json
{
  "order": "Royal crown 40, Gold nugget 30, 0x1234",
  "villager": "Raymond",
  "username": "ChoPaeng",
  "order_id": "optional_custom_id"
}
```

*Or ordering a preset:*
```json
{
  "preset": "materials",
  "username": "ChoPaeng"
}
```

*Or array format:*
```json
{
  "items": ["Royal crown 40", "Gold nugget 30"],
  "username": "ChoPaeng"
}
```

**Response:**
```json
{
  "success": true,
  "order_id": "web_a1b2c3d4",
  "user_guid": "12345678901234",
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

#### `GET /api/order/status?id={order_id}`
Polls the real-time queue position, ETA, order status, and Dodo code for a specific order.

**Response:**
```json
{
  "success": true,
  "found": true,
  "order_id": "web_a1b2c3d4",
  "username": "ChoPaeng",
  "status": "queued",
  "queue_position": 1,
  "eta": "02m:00s",
  "estimated_seconds": 120,
  "dodo_code": null,
  "island_name": "Sinta",
  "message": "Order is in queue."
}
```

#### `POST /api/order/cancel` (or `DELETE /api/order?id={order_id}`)
Cancels an order and removes it from the queue.

**Request Body:**
```json
{
  "id": "web_a1b2c3d4"
}
```

#### `GET /api/queue`
Returns the entire list of currently queued orders.

---

### 4. Drop Mode Operations

#### `POST /api/drop`
Drops items or DIY recipes on the ground around the bot.

**Request Body:**
```json
{
  "items": "Royal crown 10, Gold nugget 20",
  "type": "items",
  "username": "WebUser"
}
```

*For DIY Recipes:*
```json
{
  "items": "Ironwood kitchenette, Golden axe",
  "type": "diy",
  "username": "WebUser"
}
```

**Response:**
```json
{
  "success": true,
  "dropped_count": 30,
  "island_name": "Sinta",
  "message": "30 item drop request(s) queued and will execute momentarily."
}
```

#### `POST /api/clean`
Picks up and cleans any leftover items on the ground.

#### `POST /api/turnips`
Sets the turnip price.

**Request Body:**
```json
{
  "value": 999999999
}
```

#### `POST /api/speak`
Speaks a message in the in-game Switch chat.

**Request Body:**
```json
{
  "message": "Welcome to Sinta!"
}
```

---

## JavaScript / Web Frontend Integration Example

```javascript
// Submit an order from your website
async function submitOrder(itemsText, username) {
  const response = await fetch("http://localhost:5202/api/order", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      order: itemsText,
      username: username
    })
  });
  return await response.json();
}

// Poll order status & Dodo code
async function pollOrderStatus(orderId) {
  const response = await fetch(`http://localhost:5202/api/order/status?id=${encodeURIComponent(orderId)}`);
  const data = await response.json();
  
  if (data.status === "ready" && data.dodo_code) {
    console.log("Your Dodo Code is:", data.dodo_code);
  } else {
    console.log(`Queue Position: ${data.queue_position}, ETA: ${data.eta}`);
  }
  return data;
}

// Get Drop Mode Dodo Code
async function getDropModeDodo() {
  const response = await fetch("http://localhost:5202/api/dodo");
  return await response.json();
}
```

---

## Python / Backend Integration Example

```python
import requests

BASE_URL = "http://localhost:5202"

# 1. Get Live Status
status = requests.get(f"{BASE_URL}/api/status").json()
print("Bot Mode:", status.get("mode"), "Dodo:", status.get("dodo_code"))

# 2. Submit an Order
order_res = requests.post(f"{BASE_URL}/api/order", json={
    "order": "Royal crown 40",
    "villager": "Raymond",
    "username": "Player1"
}).json()
print("Order Queued! Position:", order_res["queue_position"], "ID:", order_res["order_id"])

# 3. Check Order Status
poll = requests.get(f"{BASE_URL}/api/order/status", params={"id": order_res["order_id"]}).json()
print("Status:", poll["status"], "Dodo:", poll.get("dodo_code"))
```

---

## TCP Socket API (Port 5201)

### `SocketAPIRequest` format:
```json
{
  "id": "123",
  "endpoint": "RequestOrder",
  "args": "{\"order\":\"Royal crown 40\",\"username\":\"Player1\"}"
}
```

### Available Socket Endpoints:
- `GetStatus`
- `GetDodo`
- `GetQueue`
- `GetOrderStatus`
- `RequestOrder`
- `CancelOrder`
- `RequestDrop`
- `RequestClean`
- `SetTurnips`
- `Speak`
- `ListPresets`
- `ListVillagers`
