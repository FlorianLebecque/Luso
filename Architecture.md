# Luso Architecture

This document defines the **target architecture** for the Luso application.  
It is the canonical reference — the codebase should be shaped to reflect it.

Luso enables synchronized multi-device light show playback in a **star topology**.  
One **Host** controls one or more **Guest** devices over a shared local network.

---

## 1. Architectural principles

- **Domain isolation** — `Room` is pure domain; it has zero knowledge of protocols or infrastructure.
- **Protocol agnosticism** — Commands flow through abstract interfaces (`ITarget`, `IDevice`, `IRoomHostSession`). No concrete protocol type ever appears in the domain or pages.
- **Technology registry** — Protocol implementations self-register via the `[RoomTechnology]` attribute. No central switch statement or factory condition needed.
- **Dependency injection** — All services are composed at application startup via MAUI's `IServiceCollection`. Pages receive services through DI.
- **Capability-aware dispatch** — Each device exposes a list of `ITarget` instances typed by `TargetKind`. Commands only reach targets that support the requested output.
- **Task-driven output** — The host controls targets through `ITask` instances managed by `ITaskOrchestrator`. Each task owns one `TargetKind` and drives `Room` commands over its lifetime. Multiple tasks can run concurrently across different target kinds.

---

## 2. Layer map

```
Pages (UI)
  └── Application Services
        └── Domain
              └── Protocol Abstractions (interfaces)
                    └── Protocol Implementations   ← self-registered via [RoomTechnology]
```

| Layer | Responsibility | Examples |
|---|---|---|
| **Pages** | User interaction, navigation | `HomePage`, `HostRoomPage`, `GuestRoomPage` |
| **Application Services** | Orchestrate domain and UI state | `IRoomFactory`, `IRoomSessionStore`, `ITaskOrchestrator`, `IGuestRosterService` |
| **Domain** | Pure session and command model | `Room`, `IDevice`, `ITarget`, `FlashCommand` |
| **Protocol Abstractions** | Interfaces the domain uses to talk to any transport | `IRoomHostSession`, `IRoomGuestSession`, `IInviteSession`, `IRoomAnnouncer`, `IRoomScanner` |
| **Protocol Implementations** | Concrete technology drivers, self-registered via `[RoomTechnology]` | `SspRoomTechnology` (room provider), `HueRoomTechnology` (invite-only device provider) |

---

## 3. Technology registry

Protocol implementations (**technologies**) self-register by decorating their entry class with `[RoomTechnology]`.

```csharp
[RoomTechnology("ssp")]
internal sealed class SspRoomTechnology : IRoomTechnology { ... }

[RoomTechnology("hue")]
internal sealed class HueRoomTechnology : IRoomTechnology { ... }
```

At startup, `IRoomTechnologyCatalog.ScanAndRegister(assembly)` discovers all decorated classes.  
`IRoomFactory` iterates `catalog.GetAll()` and uses only the capabilities each technology actually supports.

### Optional capabilities via nullable factories

Not all technologies support the same role. `IRoomTechnology` factory methods are nullable; returning `null` means the capability is not supported for that technology.

| Method | Returns `null` | Effect |
|---|---|---|
| `CreateHostSession(room)` | Yes | Technology cannot host/create a room |
| `CreateGuestSession(room)` | Yes | Technology cannot join rooms as guest session |
| `CreateAnnouncer(room)` | Yes | Room is not announced; guests join via invite/direct routing |
| `CreateScanner()` | Yes | Technology provides no protocol-level discovery signals |

When a room is created, `IRoomFactory`:
- Starts a **host session** only when `CreateHostSession(room)` returns non-null
- Starts an **invite session** only when `CreateInviteSession(room)` returns non-null
- Starts a **room announcer** only when `CreateAnnouncer(room)` returns non-null

This allows invite-only protocols (e.g., Hue bridge integration) to participate as **candidate/device providers** without ever creating or hosting a room.

Adding a new transport requires **only** a new `[RoomTechnology]` class — no changes to `RoomFactory`, `Room`, or any page.

---

## 4. Class diagrams

### 4.1 Domain model

```mermaid
classDiagram
    class Room {
        +string RoomId
        +string RoomName
        +bool IsHost
        +LocalDevice LocalDevice
        +FlashAsync(action, kind)
        +FlashDeviceAsync(deviceId, action, kind)
        +KickDeviceAsync(deviceId)
        +SendInviteAsync(device)
        +OnDeviceConnected
        +OnDeviceDisconnected
        +OnHostDisconnected
        +OnKicked
        +OnCandidateDiscovered
    }

    class IDevice {
        <<interface>>
        +string DeviceId
        +string DeviceName
        +IReadOnlyList~ITarget~ Targets
        +DisconnectAsync()
        +OnLatencyUpdated
    }

    class ITarget {
        <<interface>>
        +TargetKind Kind
        +ExecuteAsync(FlashCommand)
    }

    class TargetKind {
        <<enumeration>>
        Flashlight
        Screen
        Vibration
    }

    class FlashCommand {
        +FlashAction Action
        +long AtUnixMs
    }

    class FlashAction {
        <<enumeration>>
        On
        Off
    }

    class LocalDevice {
        +Detect() LocalDevice
        +string DeviceId
        +IReadOnlyList~ITarget~ Targets
    }

    Room "1" --> "0..*" IDevice : manages
    IDevice "1" --> "1..*" ITarget : exposes
    ITarget --> TargetKind
    ITarget ..> FlashCommand : executes
    FlashCommand --> FlashAction
    LocalDevice ..|> IDevice
```

### 4.2 Application services

```mermaid
classDiagram
    class IRoomFactory {
        <<interface>>
        +Create(roomName) Room
        +JoinAsync(room) Room
    }

    class IRoomSessionStore {
        <<interface>>
        +Room Current
        +Set(room)
        +Clear()
        +ClearAsync()
    }

    class IRoomTechnologyCatalog {
        <<interface>>
        +ScanAndRegister(assembly)
        +Get(id) IRoomTechnology
        +GetDefault() IRoomTechnology
        +GetAll() IEnumerable~IRoomTechnology~
    }

    class ITaskOrchestrator {
        <<interface>>
        +Start(task)
        +Stop(kind)
        +StopAll()
    }

    class ITask {
        <<interface>>
        +TargetKind Kind
        +StartAsync(room)
        +Stop()
    }

    class StrobeTask {
        +TargetKind Kind
        +double FrequencyHz
    }

    class AudioTask {
        +TargetKind Kind
    }

    class ManualTask {
        +TargetKind Kind
        +Fire(action)
    }

    class IGuestRosterService {
        <<interface>>
        +Guests ObservableCollection~GuestInfo~
        +Candidates ObservableCollection~IDiscoveredDevice~
        +WireRoom(room)
        +UnwireRoom(room)
        +RemoveGuest(deviceId)
        +Clear()
    }

    class RoomDiscoveryCoordinator {
        +StartAsync()
        +Stop()
        +OnRoomDiscovered
    }

    ITaskOrchestrator "1" --> "*" ITask : runs
    StrobeTask ..|> ITask
    AudioTask ..|> ITask
    ManualTask ..|> ITask
    ITaskOrchestrator ..> IRoomSessionStore : reads
    IRoomFactory ..> IRoomTechnologyCatalog : uses
    IRoomFactory ..> IRoomSessionStore : wires
    IGuestRosterService ..> IRoomSessionStore : reads
    RoomDiscoveryCoordinator ..> IRoomTechnologyCatalog : reads
```

### 4.3 Protocol abstractions

```mermaid
classDiagram
    class IRoomTechnology {
        <<interface>>
        +string TechnologyId
        +CreateHostSession(room) IRoomHostSession?
        +CreateGuestSession(room) IRoomGuestSession?
        +CreateInviteSession(room) IInviteSession?
        +CreateAnnouncer(room) IRoomAnnouncer?
        +CreateScanner() IRoomScanner?
    }

    class IRoomHostSession {
        <<interface>>
        +StartAsync()
        +StopAsync()
        +CloseAsync()
        +OnGuestConnected
        +OnGuestDisconnected
        +OnGuestLatencyUpdated
    }

    class IRoomGuestSession {
        <<interface>>
        +StartAsync()
        +StopAsync()
        +LeaveAsync()
        +OnHostDisconnected
        +OnKicked
        +OnFlashCommand
    }

    class IInviteSession {
        <<interface>>
        +StartAsync()
        +StopAsync()
        +SendInviteAsync(device)
        +OnDevicePresenceDiscovered
    }

    class IRoomAnnouncer {
        <<interface>>
        +StartAsync()
        +StopAsync()
    }

    class IRoomScanner {
        <<interface>>
        +StartAsync()
        +Stop()
        +OnRoomDiscovered
    }

    class IDiscoveredRoom {
        <<interface>>
        +string RoomId
        +string RoomName
        +string TechnologyId
    }

    class IDiscoveredDevice {
        <<interface>>
        +string DeviceId
        +string DeviceName
        +string TechnologyId
    }

    IRoomTechnology ..> IRoomHostSession : creates (nullable)
    IRoomTechnology ..> IRoomGuestSession : creates (nullable)
    IRoomTechnology ..> IInviteSession : creates (nullable)
    IRoomTechnology ..> IRoomAnnouncer : creates (nullable)
    IRoomTechnology ..> IRoomScanner : creates (nullable)
    IRoomScanner ..> IDiscoveredRoom : discovers
    IInviteSession ..> IDiscoveredDevice : discovers
```

---

## 5. Sequence diagrams

All sequence diagrams are **protocol-agnostic** — they express flows in terms of domain and application-service interfaces only.

### 5.1 Host creates a room

```mermaid
sequenceDiagram
    participant UI as HostRoomPage
    participant Factory as IRoomFactory
    participant Catalog as IRoomTechnologyCatalog
    participant Store as IRoomSessionStore
    participant Room as Room

    UI->>Factory: Create(roomName)
    Factory->>Catalog: GetAll()
    loop for each registered technology
        Factory->>Factory: hostSession = technology.CreateHostSession(room)
        opt hostSession is not null
            Factory->>Room: AddHostSession(hostSession)
        end
        Factory->>Factory: inviteSession = technology.CreateInviteSession(room)
        opt inviteSession is not null
            Factory->>Room: AddInviteSession(technologyId, inviteSession)
        end
        Factory->>Factory: announcer = technology.CreateAnnouncer(room)
        opt announcer is not null
            Factory->>Room: AddAnnouncer(announcer)
        end
    end
    Factory-->>UI: Room
    UI->>Store: Set(room)
    UI->>Room: StartAsync()
```

### 5.2 Guest discovers and joins a room

```mermaid
sequenceDiagram
    participant UI as HomePage
    participant Coordinator as RoomDiscoveryCoordinator
    participant Catalog as IRoomTechnologyCatalog
    participant Scanner as IRoomScanner
    participant Factory as IRoomFactory
    participant Store as IRoomSessionStore

    UI->>Coordinator: StartAsync()
    Coordinator->>Catalog: GetAll()
    loop for each technology
        Coordinator->>Coordinator: scanner = technology.CreateScanner()
        opt scanner is not null
            Coordinator->>Scanner: StartAsync()
            Scanner-->>Coordinator: OnRoomDiscovered(IDiscoveredRoom)
        end
    end
    Coordinator-->>UI: OnRoomDiscovered(IDiscoveredRoom)

    UI->>Factory: JoinAsync(discoveredRoom)
    Factory->>Catalog: Get(discoveredRoom.TechnologyId)
    Factory->>Factory: CreateGuestSession via technology
    Factory-->>UI: Room (guest mode)
    UI->>Store: Set(room)
```

### 5.3 Invite flow

```mermaid
sequenceDiagram
    participant HostUI as HostRoomPage
    participant Room as Room (host)
    participant Invite as IInviteSession
    participant GuestUI as HomePage (other device)

    Invite-->>Room: OnDevicePresenceDiscovered(IDiscoveredDevice)
    Room-->>HostUI: OnCandidateDiscovered(IDiscoveredDevice)
    HostUI->>Room: SendInviteAsync(device)
    Room->>Invite: SendInviteAsync(device)

    alt guest accepts
        GuestUI->>Factory: JoinAsync(invite)
        Note over GuestUI,Room: Guest session established
    else guest refuses
        Note over HostUI: Candidate stays in list
    end
```

### 5.4 Host runs a task

```mermaid
sequenceDiagram
    participant UI as HostRoomPage
    participant Orch as ITaskOrchestrator
    participant Task as ITask
    participant Room as Room
    participant Target as ITarget

    UI->>Orch: Start(task)
    Note over Orch: stops any running task for same TargetKind
    Orch->>Task: StartAsync(room)

    loop task is active
        Task->>Room: FlashAsync(action, kind)
        Room->>Room: compute AtUnixMs (now + lead-in)
        loop for each IDevice
            loop for each ITarget where Kind matches
                Room->>Target: ExecuteAsync(FlashCommand)
                Note over Target: hands off to protocol layer via delegate
            end
        end
    end

    UI->>Orch: Stop(kind)
    Orch->>Task: Stop()
```

### 5.5 Guest receives and executes a command

```mermaid
sequenceDiagram
    participant GuestSession as IRoomGuestSession
    participant LocalDevice as LocalDevice
    participant Target as ITarget

    GuestSession-->>LocalDevice: OnFlashCommand(FlashCommand)
    loop for each ITarget
        LocalDevice->>Target: ExecuteAsync(FlashCommand)
        Note over Target: waits until AtUnixMs, then fires output
    end
```

### 5.6 Host closes the room

```mermaid
sequenceDiagram
    participant UI as HostRoomPage
    participant Room as Room
    participant HostSession as IRoomHostSession
    participant Store as IRoomSessionStore

    UI->>Room: Dispose()
    Room->>HostSession: CloseAsync()
    HostSession-->>Room: OnGuestDisconnected (all guests)
    Room-->>UI: OnDeviceDisconnected (all)
    UI->>Store: ClearAsync()
```

---

## 6. Project structure

```
Luso/
├── MauiProgram.cs                     ← DI composition root
├── Pages/                             ← all pages and page-specific views
│   ├── Home/
│   └── Rooms/
├── Components/                        ← reusable UI components only
│   ├── BottomBar/
│   └── Inputs/
├── Core/                              ← core systems (protocol-agnostic)
│   ├── RoomSystem/
│   │   ├── Domain/                    ← Room, IDevice, ITarget, command models
│   │   ├── Application/               ← IRoomFactory, ITaskOrchestrator, IGuestRosterService
│   │   ├── Discovery/                 ← RoomDiscoveryCoordinator, invite orchestration
│   │   └── Contracts/                 ← IRoomTechnology, IRoomHostSession, etc.
│   ├── SessionSystem/
│   │   └── IRoomSessionStore.cs
│   ├── TaskSystem/
│   │   ├── ITask.cs
│   │   ├── ITaskOrchestrator.cs
│   │   └── Tasks/                     ← StrobeTask, AudioTask, ManualTask
│   └── RegistrySystem/
│       ├── IRoomTechnologyCatalog.cs
│       ├── RoomTechnologyRegistry.cs
│       └── RoomTechnologyAttribute.cs
└── Protocols/                         ← each protocol is self-contained
    ├── Ssp/
    │   ├── SspRoomTechnology.cs       ← [RoomTechnology("ssp")]
    │   ├── Sessions/                  ← host/guest session implementations
    │   ├── Discovery/                 ← scanner/announcer/invite transport
    │   ├── Devices/                   ← protocol-specific IDevice adapters
    │   └── Wire/                      ← CBOR/transport payload mapping
    └── Hue/
        ├── HueRoomTechnology.cs       ← invite-only device provider
        ├── Discovery/                 ← bridge and light scanning
        ├── Invite/                    ← invite/session adapter for Hue devices
        └── Devices/                   ← Hue device/target adapters
```

Rules:
- `Core` must not depend on `Protocols`.
- `Protocols` depends only on `Core.Contracts` and `Core.Domain` abstractions.
- `Pages` and `Components` must not import protocol-specific implementations.

---

## 7. Adding a new protocol

1. Create a class decorated with `[RoomTechnology("ble")]` implementing `IRoomTechnology`.
2. Implement `CreateHostSession`, `CreateGuestSession`, `CreateInviteSession`.
3. Implement `CreateAnnouncer` and `CreateScanner` — return `null` only for capabilities the technology truly does not support.
4. Done — no changes to `RoomFactory`, `Room`, or any page are needed.

**Examples:**

```csharp
// BLE — supports broadcast discovery
public IRoomAnnouncer? CreateAnnouncer(Room room) => new BleRoomAnnouncer(room);
public IRoomScanner?   CreateScanner()            => new BleRoomScanner();

// Hue — invite-only device provider (never hosts or joins rooms)
public IRoomHostSession?  CreateHostSession(Room room) => null;
public IRoomGuestSession? CreateGuestSession(Room room) => null;
public IInviteSession?    CreateInviteSession(Room room) => new HueInviteSession(room);
public IRoomAnnouncer? CreateAnnouncer(Room room) => null;
public IRoomScanner?   CreateScanner()            => new HueBridgeScanner();
```

The `[RoomTechnology]` attribute is scanned at startup by `IRoomTechnologyCatalog.ScanAndRegister(assembly)`.

---

## 8. Operational contracts (non-happy paths)

This section defines expected behavior for real-world failures and ambiguous flows.

### 8.1 Crash/disconnect handling

- **Host crash/disconnect:** Guests detect loss via heartbeat timeout and close `GuestRoom`.
- **Guest crash/disconnect:** Host detects loss via heartbeat timeout, removes the device from `Room` and from target dispatch.
- **Task cleanup on guest loss:** Running tasks are reevaluated against currently available targets. A task is removed/stopped **only** when it has zero eligible targets.

### 8.2 Join and session lifecycle

- Guest lifecycle is **fresh join only**. Rejoin/resume is out of scope for now.
- New targets/devices do not receive any implicit “current task state”. They only receive commands emitted after they are connected.
- If identity metadata changes (e.g., protocol meta such as IP), the peer is treated as a **new device**.
- Invite-only device providers (e.g., Hue bridge integration) never create rooms and never act as host room sessions.

### 8.3 Device identity

- Device identity is protocol-aware and deterministic.
- Recommended composite identity shape: `{name}:{protocol}:{protocolMeta}`.
- Example protocol metadata: SSP uses guest IP (or equivalent transport identity).
- Identity changes are modeled as new device identities; no in-place identity mutation/merge is required in current scope.

### 8.4 Execution responsibility and guarantees

- Host is an orchestrator/dispatcher only.
- Guest is responsible for command execution on a **best-effort** basis.
- Command flow is one-way; host does not require guest execution acknowledgements.
- Partial execution across guests is acceptable in current scope.
- Host UX does not show execution-disclaimer warnings; operators validate behavior by observing/hearing connected devices.

### 8.5 Capabilities and permissions

- Capabilities are declared by guest at connection time and treated as stable for the session.
- Permission/authorization failures on a guest are guest-local concerns.
- Host remains agnostic to guest runtime permission state.

### 8.6 Security ownership

- Security is owned by each protocol implementation.
- `Room` and task orchestration remain protocol-agnostic and do not enforce transport security policy directly.
- Cross-protocol shared security contracts are on hold for now.

### 8.7 Deferred topics (explicitly later)

- Cross-device clock synchronization strategy
- Synchronization tolerance/SLA targets
- Duplicate/out-of-order command handling and stronger delivery semantics
- Extended observability/telemetry contract
- Background execution hardening and reconnection strategies