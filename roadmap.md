# SyncoStronbo Roadmap

_Last updated: 2026-03-14_

This roadmap is derived from:
- [goals.md](goals.md)
- [docs/protocol.md](docs/protocol.md)
- Current code implementation under [SyncoStronbo/Features/Rooms](SyncoStronbo/Features/Rooms)

Pre-V1 policy:
- The project is still in active development (pre-V1).
- Backward compatibility is **not required** yet.
- Breaking changes to protocol and internal data models are allowed when they simplify implementation.

---

## 1) Current status snapshot

### ✅ Delivered (foundation)
- Android app with Host/Guest room lifecycle:
  - Create room, browse/join room, leave/close room
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
  - Guest list with ping
  - Kick guest
  - Invite candidate list
- Protocol documentation with packet/sequence diagrams in [docs/protocol.md](docs/protocol.md)

### 🟡 Partially delivered
- Capability awareness:
  - Capability map exists and is exchanged in `JOIN`
  - Host-side targeting by capability is not implemented yet
- Synchronized command model:
  - Manual flash command exists
  - Extended effects (screen strobe/vibration control through protocol-level effect payloads) are not completed

### ❌ Not started (from goals)
- Target groups and trigger assignment
- Predefined sequences
- Rule-based automation
- Music/FFT trigger engine integration into session commands
- Persistence/configuration management for shows, groups, rules

---

## 2) Roadmap phases

## Phase 0 — Stabilization of current core (next)
**Goal:** Make Host/Guest room operations robust under real usage.

### Scope
- Fix UX responsiveness edge cases (navigation, room startup/teardown, list updates)
- Add protocol conformance checks (version mismatch, malformed frames, duplicate invite handling)
- Improve invite reliability and anti-duplication behavior
- Add structured logs for protocol events (`ANNC`, `PRES`, `INVI`, `JOIN`, `JACK`, `JNAK`, `KICK`, `CLOS`)

### Exit criteria
- No duplicate page opens from repeated taps
- Host create/close/join/leave stays responsive on emulator and device
- Invite accept/refuse path is stable across two devices
- Protocol mismatch path is deterministic and user-visible

---

## Phase 1 — Capability-aware control (near-term)
**Goal:** Fulfill Priority 2 from goals.md.

### Scope
- Extend capability model beyond boolean baseline to output-specific constraints
- Host-side filtering/targeting UI by capability
- Command validation: host cannot send unsupported output commands to selected targets
- Improve guest execution adapters for:
  - Flashlight
  - Screen output
  - Vibration

### Protocol impact
- Evolve capability map as needed for clarity and simplicity (breaking changes allowed pre-V1)
- Add/standardize optional capability keys where needed

### Exit criteria
- Host can target only devices supporting required output
- End-to-end manual command path works for all currently supported capabilities

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

- [ ] **Stabilize invite/join UX and session transitions**
- [ ] **Capability-based target filtering in Host UI**
- [ ] **Introduce richer effect model (strobe + screen + vibration params)**
- [ ] **Implement target groups and persistence**
- [ ] **Implement predefined sequences**
- [ ] **Implement rule engine + FFT triggers**
- [ ] **Security/auth and long-session hardening**

---

## 4) Deliverable mapping to goals.md

- **Priority 1 (core synchronized session):** largely delivered, now in stabilization/hardening loop
- **Priority 2 (capability awareness):** transport delivered, control-layer targeting pending
- **Priority 3 (trigger-driven control):** planned in Phase 3
- **Priority 4 (composition/show logic):** planned in Phases 2 and 3

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
