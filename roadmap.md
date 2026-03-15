# Luso Roadmap

_Last updated: 2026-03-15_

This roadmap is derived from:
- [goals.md](goals.md)
- [Architecture.md](Architecture.md)
- [docs/protocol.md](docs/protocol.md)

Pre-V1 policy:
- The project is still in active development (pre-V1).
- Backward compatibility is **not required** yet.
- Breaking changes to protocol and internal data models are allowed when they simplify implementation.

---

## 1) Current status snapshot

### ✅ Delivered (foundation + full architecture alignment)

**Room lifecycle & SSP/1.0 transport:**
- Create room, browse/join room, leave/close room, kick guest
- CBOR messages over UDP/TCP
- Join handshake with protocol version negotiation (`JOIN`/`JACK`/`JNAK`)
- Heartbeat and disconnection handling (`PING`/`PONG`, timeout)
- Host actions: `FLSH`, `KICK`, `CLOS`
- Invite workflow: `PRES`, `INVI`, `INVR`

**Host UI:**
- Guest list with latency/ping display
- Kick guest, invite candidate list
- 16-pad per-guest manual trigger (routed through `ManualTask` → `ITaskOrchestrator`)
- Strobe On mode via `StrobeTask` (configurable frequency)
- Audio-driven Auto mode via `AudioTask` (level threshold)

**Domain architecture (aligned to Architecture.md):**
- `Room` is pure domain — zero protocol/infrastructure imports
- Command flow: `Room → IDevice.Targets → ITarget.ExecuteAsync(FlashCommand)` → protocol delegate
- Typed `FlashCommand(FlashAction, AtUnixMs)` — no magic strings
- `TargetKind` enum: `Flashlight`, `Screen`, `Vibration`
- `LocalDevice.Detect()` auto-discovers platform targets at runtime

**Protocol abstraction layer (`Core/RoomSystem/Contracts/`):**
- `IRoomTechnology` with nullable factory pattern (`CreateHostSession(Room)`, `CreateGuestSession(Room)`, etc.)
- `IRoomHostSession`: `StartAsync()`, `StopAsync()`, `CloseAsync()`, `OnGuestConnected/Disconnected/LatencyUpdated`
- `IRoomGuestSession`: `StartAsync()`, `StopAsync()`, `LeaveAsync()`, `OnFlashCommand`, `OnHostDisconnected`, `OnKicked`
- `IRoomAnnouncer`: `StartAsync()`, `StopAsync()`
- `IRoomScanner`: `StartAsync()`, `Stop()`
- `IInviteSession`, `IDiscoveredRoom`, `IDiscoveredDevice`, `IRoomInvite`

**Technology self-registration:**
- `[RoomTechnology]` attribute + `IRoomTechnologyCatalog.ScanAndRegister(assembly)`
- `SspRoomTechnology` self-registers; no factory switch needed
- Adding a new protocol requires only a new `[RoomTechnology]` class

**Application services:**
- `IRoomFactory` / `RoomFactory` — iterates catalog, wires sessions, calls `Room.StartAsync()`
- `ITaskOrchestrator` / `TaskOrchestrator` — one task per `TargetKind`
- `StrobeTask`, `AudioTask`, `ManualTask` (all routed through orchestrator)
- `IGuestRosterService` / `GuestRosterService` with latency tracking
- `IRoomSessionStore` / `RoomSessionStore` (replaces old static state)
- `RoomDiscoveryCoordinator` with `StartAsync()` / `Stop()`
- All host output flows through `ITaskOrchestrator → ITask → Room` — no bypasses

**SSP protocol (`Protocols/Ssp/`):**
- Organized into `Sessions/`, `Discovery/`, `Devices/`, `Wire/` subdirectories
- `SspHostSession` defers TCP listener to `StartAsync()`
- `SspGuestSession` accepts `RoomAnnouncement`, connects in `StartAsync()`
- `SspRoomAnnouncer` / `SspRoomScanner` with async lifecycle
- `SspDevice` / `SspRemoteTarget` use `DeviceName` and `ExecuteAsync(FlashCommand)`

**UI alignment:**
- `HostRoomPage` routes pad through `ManualTask` via `ITaskOrchestrator`
- `GuestRoomPage` has no direct `Flashlight.Default` calls
- `HomePage` / `BrowseRoomsPage` use `coordinator.StartAsync()`
- `CreateRoomPage` calls `room.StartAsync()` after `factory.Create()`
- All pages depend only on domain interfaces, never protocol types

**Cleanup completed:**
- `LightController` deleted (replaced by `ITarget` dispatch)
- All `Flashlight.Default` bypasses eliminated from tasks and pages
- Old flat SSP files and static `RoomSession` removed

### 🟡 Partially delivered
- **Screen strobe:** `ScreenTarget` present in domain and wired into `LocalDevice.Targets` — guest-page full-screen color overlay rendering not yet connected to `ExecuteAsync`
- **Audio trigger:** Level-threshold Auto mode functional — full configurable FFT trigger engine (frequency bands, thresholds, conditions) not implemented
- **Capability UI:** Domain + transport fully capability-aware — host UI does not yet expose per-capability targeting selector

### ❌ Not started
- Target groups and trigger assignment
- Predefined sequences
- Rule-based automation
- Persistence/configuration management for shows, groups, rules
- Security / room access control
- Hue protocol integration (invite-only device provider pattern)

---

## 2) Roadmap phases

## Phase 0 — Screen strobe + stabilization (active)
**Goal:** Complete the last output target and harden real-world usage.

### Scope
- Connect `ScreenTarget.ExecuteAsync` to a full-screen color overlay in the guest page
- Add protocol conformance checks (version mismatch, malformed frames, duplicate invite handling)
- Improve invite reliability and anti-duplication behavior
- Add structured logs for protocol events (`ANNC`, `PRES`, `INVI`, `JOIN`, `JACK`, `JNAK`, `KICK`, `CLOS`)
- Fix UX responsiveness edge cases (navigation, room startup/teardown, list updates)

### Exit criteria
- Guest screen strobe renders correctly via `ScreenTarget`
- No duplicate page opens from repeated taps
- Host create/close/join/leave stays responsive on emulator and device
- Invite accept/refuse path is stable across two devices
- Protocol mismatch path is deterministic and user-visible

---

## Phase 1 — Capability-aware control UI + strobe model (near-term)
**Goal:** Surface the existing capability-aware domain layer in the host UI, and introduce a richer effect model.

### Context
The domain and transport layers are already fully capability-aware (`GuestCapabilities` → `ITarget` per device).
What remains is exposing this to the user and extending the effect payload beyond discrete on/off.

### Scope
- Host UI capability targeting selector: allow host to choose which output kind (flashlight / screen / vibration) to trigger
- Command validation: prevent sending to devices whose `Targets` list lacks the required `TargetKind`
- Introduce strobe effect model: duration, frequency, duty cycle params in the command payload
- Extend `FlashCommand` or introduce a new `StrobeCommand` to carry strobe parameters
- Map strobe command to protocol wire representation at SSP boundary only

### Protocol impact
- Extend `FLSH` payload or add new effect message type — breaking changes allowed pre-V1

### Exit criteria
- Host can select output kind per triggered command
- Strobe with duration/frequency/duty cycle executes deterministically on flashlight and screen targets

---

## Phase 2 — Show composition primitives (mid-term)
**Goal:** Build minimum light-show composition layer.

### Scope
- Target groups (Host/Guest/both membership)
- Reusable effects with parameters (including strobe: duration/frequency/duty cycle)
- Predefined sequences (ordered timed commands)
- Basic local persistence for groups/effects/sequences

### Protocol impact
- Extend command payload format beyond basic `FLSH` action
- Replace or reshape existing `FLSH` flow if needed to support richer effect payloads

### Exit criteria
- User can create groups and launch saved sequence against selected groups
- Sequence playback behaves deterministically across guests

---

## Phase 3 — Trigger engine v1 (mid/long-term)
**Goal:** Deliver Priority 3 trigger-driven control.

### Scope
- Manual trigger mapping to groups/effects/sequences
- Rule-based trigger engine (condition → action)
- FFT/music trigger ingestion with threshold/configurable events
- Trigger execution state model and observability

### Exit criteria
- Trigger definitions can be created/edited/launched
- Rule and FFT triggers dispatch actions to target groups
- Trigger execution logs are traceable and debuggable

---

## Phase 4 — Protocol expansion (long-term)
**Goal:** Prove multi-technology architecture with a second protocol.

### Scope
- Hue bridge integration as invite-only device provider (`HueRoomTechnology`)
- `CreateHostSession` / `CreateGuestSession` → `null`; `CreateInviteSession` → `HueInviteSession`
- Hue lights appear as `IDevice` with `ScreenTarget` (RGB output)
- No changes to `Room`, `RoomFactory`, or any page required

### Exit criteria
- Hue bulbs controllable from the host pad alongside phone guests
- Architecture proven for the invite-only device-provider pattern end-to-end

---

## Phase 5 — Product hardening and scale-up (long-term)
**Goal:** Production readiness and extensibility.

### Scope
- Security model (room access control/authentication)
- Conflict handling and reconnection policy
- Performance tuning for larger guest counts
- Export/import of show configuration
- Background execution and power-management tuning
- External integration boundary design (future APIs, bridge apps)

### Exit criteria
- Security and persistence model documented + implemented
- Stable operation for target device count and session duration
- Reproducible deployment and diagnostics workflow

---

## 3) Prioritized backlog (next execution order)

- [ ] **P0** Connect `ScreenTarget.ExecuteAsync` to guest-page full-screen strobe overlay
- [ ] **P0** Stabilize invite/join UX and session transitions
- [ ] **P0** Add protocol diagnostics and structured logging
- [ ] **P1** Host UI: capability-based output selector (flashlight / screen / vibration)
- [ ] **P1** Introduce strobe effect model (duration / frequency / duty cycle)
- [ ] **P2** Implement target groups with persistence
- [ ] **P2** Implement predefined sequence model and playback
- [ ] **P3** Implement rule engine + configurable FFT trigger dispatch
- [ ] **P4** Hue bridge integration (invite-only device provider)
- [ ] **P5** Security/auth and long-session hardening

---

## 4) Deliverable mapping to goals.md

- **Priority 1 (core synchronized session):** ✅ fully delivered — room lifecycle, SSP transport, synchronized `FlashCommand` with timestamp, guest execution, kick, invite all working.
- **Priority 2 (capability awareness):** ✅ domain + transport fully delivered (capabilities exchanged, typed `ITarget` per device, `Room.FlashAsync` filters by `TargetKind`); host UI targeting selector pending (Phase 1).
- **Priority 3 (trigger-driven control):** manual ✅ (`ManualTask`); strobe ✅ (`StrobeTask`); audio level–threshold ✅ (`AudioTask`); full FFT/rule engine ❌ planned Phase 3.
- **Priority 4 (composition/show logic):** planned Phases 2–3.

---

## 5) Protocol alignment checkpoints

At the end of each phase, ensure [docs/protocol.md](docs/protocol.md) is updated with:
- Message tables
- Packet diagrams
- Sequence diagrams
- Error/refusal semantics

This keeps implementation and protocol documentation synchronized.

---

## 6) GitHub Projects-ready tracking

Use this section as a direct source for GitHub Issues and GitHub Projects fields.

### Suggested fields
- **Milestone:** `M0 Screen Strobe + Stabilization`, `M1 Capability Control`, `M2 Composition`, `M3 Trigger Engine`, `M4 Hue Integration`, `M5 Hardening`
- **Labels:** `roadmap`, `phase:*`, `area:protocol`, `area:ui`, `area:networking`, `area:audio`, `priority:*`
- **Status:** `Backlog`, `Ready`, `In Progress`, `Blocked`, `Done`

### Issue-ready work items

- [ ] `M0` Connect ScreenTarget to guest-page full-screen color overlay
  **Labels:** `roadmap`, `phase:0`, `area:ui`, `priority:high`
  **Definition of done:** guest page renders full-screen color strobe driven by `ScreenTarget.ExecuteAsync`.

- [ ] `M0` Stabilize room navigation and session transitions
  **Labels:** `roadmap`, `phase:0`, `area:ui`, `priority:high`
  **Definition of done:** no duplicate page opens; create/close/leave actions are responsive under repeated taps.

- [ ] `M0` Harden invite workflow reliability
  **Labels:** `roadmap`, `phase:0`, `area:networking`, `area:protocol`, `priority:high`
  **Definition of done:** invite accept/refuse flows work consistently across two devices; duplicate/inflight invite handling is deterministic.

- [ ] `M0` Add protocol diagnostics and error surfacing
  **Labels:** `roadmap`, `phase:0`, `area:protocol`, `priority:medium`
  **Definition of done:** structured logs for core message flow and visible user errors for protocol mismatches.

- [ ] `M1` Host UI capability-based output selector
  **Labels:** `roadmap`, `phase:1`, `area:ui`, `priority:high`
  **Definition of done:** Host can select which `TargetKind` to trigger; commands only reach compatible devices.

- [ ] `M1` Strobe effect model (duration / frequency / duty cycle)
  **Labels:** `roadmap`, `phase:1`, `area:protocol`, `priority:high`
  **Definition of done:** strobe effect with full parameters executes deterministically on flashlight and screen targets.

- [ ] `M2` Implement target groups with persistence
  **Labels:** `roadmap`, `phase:2`, `area:ui`, `priority:high`
  **Definition of done:** groups can be created/edited/saved and used as command targets.

- [ ] `M2` Implement predefined sequence model and playback
  **Labels:** `roadmap`, `phase:2`, `area:protocol`, `priority:high`
  **Definition of done:** saved ordered effects execute with deterministic timing across guests.

- [ ] `M3` Implement rule engine v1
  **Labels:** `roadmap`, `phase:3`, `area:protocol`, `priority:medium`
  **Definition of done:** condition-to-action rules can be configured and executed.

- [ ] `M3` Integrate FFT/music trigger dispatch
  **Labels:** `roadmap`, `phase:3`, `area:audio`, `area:protocol`, `priority:medium`
  **Definition of done:** audio events can trigger groups/effects with observable execution logs.

- [ ] `M4` Hue bridge integration
  **Labels:** `roadmap`, `phase:4`, `area:networking`, `priority:medium`
  **Definition of done:** Hue bulbs controllable as `IDevice` instances from the host pad.

- [ ] `M5` Implement auth/security baseline for rooms
  **Labels:** `roadmap`, `phase:5`, `area:networking`, `priority:high`
  **Definition of done:** room access control implemented and documented.

- [ ] `M5` Export/import show configurations
  **Labels:** `roadmap`, `phase:5`, `area:ui`, `priority:medium`
  **Definition of done:** users can save/load reusable show configuration bundles.
