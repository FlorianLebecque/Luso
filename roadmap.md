# Luso Roadmap

_Last updated: 2026-03-15_

This roadmap is derived from:
- [goals.md](goals.md)
- [docs/protocol.md](docs/protocol.md)
- Current code implementation under [Luso/Features/Rooms](Luso/Features/Rooms)

Pre-V1 policy:
- The project is still in active development (pre-V1).
- Backward compatibility is **not required** yet.
- Breaking changes to protocol and internal data models are allowed when they simplify implementation.

---

## 1) Current status snapshot

### ✅ Delivered (foundation + architecture rework)
- Android app with Host/Guest room lifecycle:
  - Create room, browse/join room, leave/close room, kick guest
- SSP/1.0 transport implemented:
  - CBOR messages over UDP/TCP
  - Join handshake with protocol version negotiation (`JOIN`/`JACK`/`JNAK`)
  - Heartbeat and disconnection handling (`PING`/`PONG`, timeout handling)
  - Host actions: `FLSH`, `KICK`, `CLOS`
- Invite workflow implemented:
  - Guest presence broadcast (`PRES`)
  - Host invite (`INVI`)
  - Guest refusal (`INVR`)
- Host operational UX:
  - Guest list with latency/ping display
  - Kick guest
  - Invite candidate list
  - 16-pad per-guest manual trigger
  - Audio-driven Auto mode (level threshold via `AudioAnalyser`)
- Guest capability exchange:
  - `GuestCapabilities` (flashlight, screen, vibration + dimensions) declared in `JOIN`
  - `SspDevice.BuildTargets` maps capabilities to typed `SspRemoteTarget` instances at connect time
  - `LocalDevice.Detect()` auto-discovers platform targets at runtime
- Capability-aware command dispatch (domain level):
  - `Room.FlashAsync` dispatches through `IDevice.Targets` filtered by `TargetKind`
  - `Room.FlashDeviceAsync` targets a single device by ID
  - `Room.KickDeviceAsync` routes through `IDevice.DisconnectAsync`
- Guest-side output execution:
  - `FlashlightTarget` — precise timing via `cmd.AtUnixMs`, `Flashlight.Default`
  - `VibrationTarget` — 200 ms vibration on action `"on"`
  - `RgbLightTarget` — wired in domain layer (guest-page render pending)
- Architecture rework (2026-03-15):
  - `Room` is pure domain — zero protocol/infrastructure imports
  - Commands flow: `Room` → `IDevice.Targets` → `ITarget.ExecuteAsync` → protocol delegate
  - `IRoomHostSession` / `IRoomGuestSession` slimmed to lifecycle events only
  - `SspRemoteTarget` encapsulates the socket call behind a `Func<FlashCommand,Task>` delegate
  - `SspGuestSession` handles flash dispatch internally — guest page has no command handling
  - `RoomFactory` is the sole entry point for `RoomTechnologyRegistry` in the Features layer
  - Multi-technology pattern: `[RoomTechnology]`-decorated classes self-register; `RoomFactory.Create` starts sessions for all of them
  - `IDiscoveredDevice.TechnologyId` + `IDiscoveredRoom.TechnologyId` enable protocol-agnostic invite routing and join
- Protocol documentation with packet/sequence diagrams in [docs/protocol.md](docs/protocol.md)
- Architecture review + class diagrams in [docs/architecture-review.md](docs/architecture-review.md)

### 🟡 Partially delivered
- Capability awareness:
  - Domain + transport layers fully capability-aware
  - Host UI does not yet expose per-capability targeting selector (flashlight vs. screen vs. vibration)
- Synchronized command model:
  - `FlashCommand` with `Action` + `AtUnixMs` timestamp implemented
  - Discrete on/off synchronization works end-to-end
  - Strobe pattern model (duration/frequency/duty cycle) not yet implemented
- Screen strobe:
  - `RgbLightTarget` present in domain and wired into `LocalDevice.Targets`
  - Guest-page rendering (full-screen color overlay) not yet connected to `ExecuteAsync`
- Audio trigger:
  - Level-threshold Auto mode in host UI is functional
  - Proper configurable FFT trigger engine (frequency bands, thresholds, conditions) not implemented

### ❌ Not started
- Target groups and trigger assignment
- Predefined sequences
- Rule-based automation
- Persistence/configuration management for shows, groups, rules
- Security / room access control

---

## 2) Roadmap phases

## Phase 0 — Stabilization of current core (active)
**Goal:** Make Host/Guest room operations robust under real usage.

### Scope
- Fix UX responsiveness edge cases (navigation, room startup/teardown, list updates)
- Add protocol conformance checks (version mismatch, malformed frames, duplicate invite handling)
- Improve invite reliability and anti-duplication behavior
- Add structured logs for protocol events (`ANNC`, `PRES`, `INVI`, `JOIN`, `JACK`, `JNAK`, `KICK`, `CLOS`)
- Connect `RgbLightTarget.ExecuteAsync` to a full-screen color overlay in the guest page
- Route Auto-mode local flashlight through `Room.FlashAsync` to unify all output dispatch (currently bypassed)
- Replace `async void` fire-and-forget patterns in `LightController` with cancellable `Task` loop

### Exit criteria
- No duplicate page opens from repeated taps
- Host create/close/join/leave stays responsive on emulator and device
- Invite accept/refuse path is stable across two devices
- Protocol mismatch path is deterministic and user-visible
- All output paths (host + guest) route through `Room.FlashAsync` / `ITarget.ExecuteAsync`
- Guest screen strobe renders correctly

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
- Replace magic strings `"on"` / `"off"` with a typed `FlashAction` enum in the domain

### Protocol impact
- Extend `FLSH` payload or add new effect message type — breaking changes allowed pre-V1

### Exit criteria
- Host can select output kind per triggered command
- Strobe with duration/frequency/duty cycle executes deterministically on flashlight and screen targets
- No magic `"on"`/`"off"` strings remain in domain layer

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

## Phase 4 — Product hardening and scale-up (long-term)
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

- [ ] **P0** Connect `RgbLightTarget.ExecuteAsync` to guest-page full-screen strobe overlay
- [ ] **P0** Route Auto-mode local output through `Room.FlashAsync` (remove direct `Flashlight.Default` calls)
- [ ] **P0** Stabilize invite/join UX and session transitions
- [ ] **P0** Replace `LightController` busy-loop with cancellable async loop
- [ ] **P1** Replace `"on"`/`"off"` magic strings with typed `FlashAction` enum
- [ ] **P1** Host UI: capability-based output selector (flashlight / screen / vibration)
- [ ] **P1** Introduce strobe effect model (duration / frequency / duty cycle)
- [ ] **P2** Implement target groups with persistence
- [ ] **P2** Implement predefined sequence model and playback
- [ ] **P3** Implement rule engine + configurable FFT trigger dispatch
- [ ] **P4** Security/auth and long-session hardening

---

## 4) Deliverable mapping to goals.md

- **Priority 1 (core synchronized session):** fully delivered — room lifecycle, SSP transport, synchronized `FlashCommand` with timestamp, guest execution, kick, invite all working.
- **Priority 2 (capability awareness):** domain + transport fully delivered (capabilities exchanged, typed `ITarget` per device, `Room.FlashAsync` filters by `TargetKind`); host UI targeting selector and strobe model pending (Phase 1).
- **Priority 3 (trigger-driven control):** manual triggers ✅; audio level–threshold Auto mode 🟡; full FFT/rule engine ❌ planned Phase 3.
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
- **Milestone:** `M0 Stabilization`, `M1 Capability Control`, `M2 Composition`, `M3 Trigger Engine`, `M4 Hardening`
- **Labels:** `roadmap`, `phase:*`, `area:protocol`, `area:ui`, `area:networking`, `area:audio`, `priority:*`
- **Status:** `Backlog`, `Ready`, `In Progress`, `Blocked`, `Done`

### Issue-ready work items

- [ ] `M0` Stabilize room navigation and close responsiveness  
  **Labels:** `roadmap`, `phase:0`, `area:ui`, `priority:high`  
  **Definition of done:** no duplicate page opens; create/close/leave actions are responsive under repeated taps.

- [ ] `M0` Harden invite workflow reliability  
  **Labels:** `roadmap`, `phase:0`, `area:networking`, `area:protocol`, `priority:high`  
  **Definition of done:** invite accept/refuse flows work consistently across two devices; duplicate/inflight invite handling is deterministic.

- [ ] `M0` Add protocol diagnostics and error surfacing  
  **Labels:** `roadmap`, `phase:0`, `area:protocol`, `priority:medium`  
  **Definition of done:** structured logs for core message flow (`ANNC`, `PRES`, `INVI`, `JOIN`, `JACK`, `JNAK`, `KICK`, `CLOS`) and visible user errors.

- [ ] `M1` Implement capability-based target filtering  
  **Labels:** `roadmap`, `phase:1`, `area:ui`, `area:protocol`, `priority:high`  
  **Definition of done:** Host can only select/send commands to compatible devices.

- [ ] `M1` Extend output adapters (flash/screen/vibration)  
  **Labels:** `roadmap`, `phase:1`, `area:networking`, `priority:high`  
  **Definition of done:** all three output categories execute reliably from Host commands.

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

- [ ] `M4` Implement auth/security baseline for rooms  
  **Labels:** `roadmap`, `phase:4`, `area:networking`, `priority:high`  
  **Definition of done:** room access control implemented and documented.

- [ ] `M4` Export/import show configurations  
  **Labels:** `roadmap`, `phase:4`, `area:ui`, `priority:medium`  
  **Definition of done:** users can save/load reusable show configuration bundles.
