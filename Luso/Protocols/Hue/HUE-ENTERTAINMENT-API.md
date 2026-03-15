# Philips Hue Entertainment API

> Source: https://developers.meethue.com/develop/hue-entertainment/hue-entertainment-api/

---

## Overview

Standard Zigbee is reliable for scenes and automation but too slow for synchronized live effects.  
The Entertainment API adds a **parallel UDP/DTLS streaming path** that bypasses normal Zigbee retries.

### How a command is delivered

1. Up to **10 light channels** packaged into a custom Zigbee message
2. Unicasted to an auto-chosen **proxy node** in the entertainment room
3. Proxy does a **non-repeating MAC-layer broadcast** — all nearby lights hear it
4. Lights act immediately — **no retries** (a skipped frame beats a late one)
5. Result: **25 Hz to any light**, spatial effects across multiple lights, zero congestion

---

## Authentication (one-time setup)

### Step 1 — Register app, get `username` + `clientkey`

```
POST https://<ip>/api
Body: { "devicetype": "myapp#mydevice", "generateclientkey": true }
```

> Press the **physical link button** on the bridge first.

Response:
```json
{
  "success": {
    "username": "myzFXhsLU5Wg10eBithGE-LFikgjC7Q7SEGZsoEf",
    "clientkey": "E3B550C65F78022EFD9E52E28378583"
  }
}
```

| Field | Usage |
|---|---|
| `username` | `hue-application-key` header in all REST calls |
| `clientkey` | 32-char ASCII hex → decode to 16 bytes → DTLS **PSK**. Shown **once only**, never expires. |

### Step 2 — Get `hue-application-id`

```
GET https://<ip>/auth/v1
Header: hue-application-key: <username>
```

Returns `hue-application-id` in the response **header** — used as the **DTLS PSK identity**.

---

## Entertainment Configuration

### List areas

```
GET https://<ip>/clip/v2/resource/entertainment_configuration
Header: hue-application-key: <username>
```

Example area object:
```json
{
  "id": "1a8d99cc-967b-44f2-9202-43f976c0fa6b",
  "type": "entertainment_configuration",
  "metadata": { "name": "Entertainment area 1" },
  "configuration_type": "screen",
  "channels": [
    { "channel_id": 0, "position": { "x": -0.6, "y": 0.8, "z": 0.0 } },
    { "channel_id": 1, "position": { "x":  0.6, "y": 0.8, "z": 0.0 } }
  ],
  "status": "inactive"
}
```

- Position axes: `x` = left/right, `y` = front/back, `z` = up/down, range −1..+1
- `active_streamer` field shows which app is currently streaming

### Start / stop streaming

```
PUT https://<ip>/clip/v2/resource/entertainment_configuration/<id>
Header: hue-application-key: <username>
Body: { "action": "start" }   // or "stop"
```

Only **one stream can be active** per area at a time.

---

## DTLS Handshake

| Parameter | Value |
|---|---|
| Port | UDP **2100** |
| Protocol version | DTLS **1.2** only |
| Cipher suite | `TLS_PSK_WITH_AES_128_GCM_SHA256` |
| PSK | 16-byte binary decoded from 32-char hex `clientkey` |
| PSK identity | `hue-application-id` (UTF-8 string) |

Recommended library: **mbedtls** (or any standard DTLS 1.2 implementation).

### DTLS error codes

| Error | Type | Meaning |
|---|---|---|
| `protocol_version` | Fatal | DTLS version not supported |
| `insufficient_security` | Fatal | Higher cipher required |
| `handshake_failure` | Fatal | Other security parameter mismatch |
| `unknown_psk_identity` | Fatal | Unknown PSK identity |
| `decrypt_error` | Fatal | Invalid PSK |
| `close_notify` | Fatal | Bridge disconnecting (e.g. streaming disabled via CLIP) |
| `user_cancelled` | Warning | Max sessions already active |

---

## Stream Message Format

After DTLS handshake, send encrypted UDP datagrams to **port 2100**.

### Layout

```
[ 9 bytes  ] Protocol name  →  "HueStream" (literal ASCII)
[ 2 bytes  ] Version        →  0x02 0x00  (v2.0)
[ 1 byte   ] Sequence ID    →  any value (ignored by bridge)
[ 2 bytes  ] Reserved       →  0x00 0x00
[ 1 byte   ] Color space    →  0x00 = RGB | 0x01 = XY+Brightness
[ 1 byte   ] Reserved       →  0x00
[36 bytes  ] Config UUID    →  ASCII UUID of the entertainment configuration
[ 7 × N   ] Channel data    →  up to 20 channels
```

### Per-channel slot (7 bytes)

```
[ 1 byte  ] channel_id
[ 2 bytes ] component 1  (R or X)  — 16-bit big-endian
[ 2 bytes ] component 2  (G or Y)  — 16-bit big-endian
[ 2 bytes ] component 3  (B or Bri) — 16-bit big-endian
```

### Example packet

```c
// Header
'H','u','e','S','t','r','e','a','m',   // protocol
0x02, 0x00,                             // version 2.0
0x07,                                   // sequence number 7
0x00, 0x00,                             // reserved
0x00,                                   // color space: RGB
0x00,                                   // reserved

// Config UUID (36 ASCII bytes)
"1a8d99cc-967b-44f2-9202-43f976c0fa6b",

// Channel 0 — full red
0x00,
0xFF,0xFF,  0x00,0x00,  0x00,0x00,

// Channel 1 — full blue
0x01,
0x00,0x00,  0x00,0x00,  0xFF,0xFF,
```

### Color spaces

| Mode | When to use |
|---|---|
| **RGB** (`0x00`) | Simple; widest per-bulb range |
| **XY+Brightness** (`0x01`) | Cross-gamut consistency; matches display RGB accurately |

- For XY: `0x0000` = 0.0, `0xFFFF` = 1.0 (internally 12-bit for x/y, 11-bit for brightness)
- RGB→XY conversion is done by the bridge

---

## Bridge Discovery

### Option A — mDNS / ZeroConf (recommended, local-only)

The bridge advertises itself as `_hue._tcp.local.`

```csharp
// Package: Zeroconf
// dotnet add package Zeroconf

using Zeroconf;

IReadOnlyList<IZeroconfHost> results =
    await ZeroconfResolver.ResolveAsync("_hue._tcp.local.");

foreach (var host in results)
    Console.WriteLine($"Bridge: {host.DisplayName} @ {host.IPAddress}");
```

> **Android note:** Wi-Fi AP client isolation blocks mDNS multicast between devices —
> `WifiManager.MulticastLock` must be acquired before scanning, and it still may not
> work on networks with strict isolation. Use the LAN scan fallback in that case.

### Option B — LAN HTTP scan (HueApi built-in)

```csharp
using HueApi.BridgeLocator;

var locator = new LocalNetworkScanBridgeLocator();
var bridges = await locator.LocateBridgesAsync(CancellationToken.None);
// Each result: bridge.BridgeId, bridge.IpAddress
```

### Option C — Cached IP fast-path

Store `bridgeId → ip` after first successful connection.  
On next launch probe `GET http://<cached-ip>/api/config` with a 500 ms timeout.  
If it responds, skip the full scan entirely.

---

## Best Practices

- **Stream at 50–60 Hz** — UDP is lossy; keep repeating the last state if nothing changed
- Bridge ZigBee output maxes at **25 Hz**; design visible effect transitions at **< 12.5 Hz**
- Regular HTTP API commands still execute during streaming but are invisible (overwritten every frame)
- Only **one streaming session** per bridge at a time
- Always send `{ "action": "stop" }` when done
- Bridge auto-closes the session after **10 seconds** of inactivity
- Warn users: fast-changing light effects may trigger **photosensitive epilepsy**

---

## Implementation Notes (this codebase)

| Class | Role |
|---|---|
| `HueInviteSession` | Discovery (cached fast-path → LAN scan) + pairing (link-button poll) |
| `HueBridgeAuth` | Persists `username`/apikey and known bridge IPs via MAUI `Preferences` |
| `HueBridgeCommandBuffer` | Per-bridge UDP-style backpressure: latest-wins, one-in-flight per light |
| `HueLightTarget` | `ITarget` wrapper around a single bulb; schedules into `HueBridgeCommandBuffer` |
| `HueBridgeDevice` | Groups all `HueLightTarget`s for one bridge into one `IDevice` |

> The current integration uses the **CLIP v2 REST API** (`HueApi 3.0.0`) for individual  
> light on/off commands, not the Entertainment streaming UDP API.  
> The Entertainment streaming path (DTLS + UDP) would be the next step for  
> sub-100 ms synchronized multi-light effects.
