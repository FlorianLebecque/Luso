# Refactor Plan (Incremental)

This plan is optimized for low-risk migration from current implementation to the architecture in [Architecture.md](../Architecture.md).

## Current execution status
- ✅ Slice 1 completed (structure scaffolding + folder migration + build pass).
- ✅ Slice 2 completed (nullable capability contracts aligned).
- ✅ Slice 3 completed (factory/coordinator capability filtering hardened for optional scanners/sessions).
- ✅ Slice 4 completed (task orchestration abstraction wired; host behavior preserved).
- ✅ Slice 5 completed (SSP remains self-contained under `Protocols/Ssp`; no `Core`/`Pages` protocol coupling).
- ✅ Architecture convergence pass completed (announcer capability added + `Room.FlashAsync(action, kind)` aligned with architecture contract).

---

## Completed slices

### Slice 1 — Structure scaffolding + namespace alignment
- Create target top-level folders (`Pages`, `Components`, `Core`, `Protocols`) and move files with minimal logic change.
- Keep adapters/forwarders only where needed to preserve behavior.
- Outcome: structure matches architecture, behavior unchanged.

### Slice 2 — Core contracts stabilization
- Consolidate protocol contracts under `Core/.../Contracts`.
- Make nullable factory capability model consistent (`CreateHostSession?`, `CreateGuestSession?`, etc.).
- Outcome: invite-only protocol support ready without behavior change.

### Slice 3 — Room creation/join capability filtering
- Update factory/coordinator paths to ignore null capabilities safely.
- Ensure room-provider and invite-only device-provider protocols both fit.
- Outcome: architecture-compatible creation/discovery pipeline.

### Slice 4 — Task orchestration abstraction
- Replace flash-specific orchestrator usage with task orchestrator abstractions.
- Keep existing flash behavior via initial task implementations.
- Outcome: generic task engine shape with existing behavior preserved.

### Slice 5 — Protocol module hard boundary
- Move SSP entirely under `Protocols/Ssp` with local wire mapping and adapters.
- Outcome: protocol self-contained module.

---

## Remaining slices (implementation → architecture alignment)

The clashes below were identified in a full architecture review on 2026-03-15.
Each slice maps to one or more numbered clashes from that review.

---

### Slice 6 — Eliminate direct `Flashlight.Default` / `LightController` bypasses
**Clashes addressed:** #10 (AudioTask bypass), #11 (HostRoomPage host-pad bypass), #14 (StrobeTask LightController bypass)

The architecture mandates that **all output flows through `Room → IDevice → ITarget.ExecuteAsync`**.
Three call sites currently bypass this:

| Call site | Current behavior | Target behavior |
|---|---|---|
| `AudioTask.StartAsync` | Calls `Flashlight.Default.TurnOnAsync/Off` directly alongside `room.FlashAsync` | Remove direct flashlight calls; rely solely on `room.FlashAsync` which dispatches to `LocalDevice.Targets` |
| `StrobeTask.StartAsync` | Creates `LightController.GetInstance()` and runs its own strobe loop for local flashlight | Remove `LightController` usage; rely solely on `room.FlashAsync` dispatching to local `FlashlightTarget` |
| `HostRoomPage.OnPadPressed/Released` | When `CommandParameter` is null (host device), calls `Flashlight.Default` directly | Route through `room.FlashDeviceAsync(localDevice.DeviceId, action)` instead |

#### Scope
- `AudioTask`: remove all `Flashlight.Default.*` calls; `room.FlashAsync` already reaches `LocalDevice.FlashlightTarget`.
- `StrobeTask`: remove `LightController` field and all usages; verify `room.FlashAsync` delivers the same strobe cadence to local targets.
- `HostRoomPage`: replace `Flashlight.Default` calls in the null-parameter pad path with `room.FlashDeviceAsync(room.LocalDevice!.DeviceId, action)`.
- Evaluate whether `LightController` is still referenced anywhere else; if not, delete the file.

#### Validation
- Build green.
- Host pad press/release still toggles local flashlight (now via domain path).
- Strobe On mode still drives local + remote flashlights in sync.
- Auto mode still drives local + remote flashlights from audio level.
- No duplicate `Flashlight.Default` calls remain in `Core/` or `Pages/`.

---

### Slice 7 — Extract `SspRoomAnnouncer` from `SspHostSession`
**Clash addressed:** #12

Architecture.md §5.1 requires the announcer to be a separate `IRoomAnnouncer` returned by `CreateAnnouncer()`.
Currently `SspHostSession` creates a `UdpRoomDiscovery` internally and starts announcing in its constructor, while `SspRoomTechnology.CreateAnnouncer()` returns `null`.

#### Scope
- Create `SspRoomAnnouncer : IRoomAnnouncer` that wraps `UdpRoomDiscovery.StartAnnouncing` / `StopAnnouncing`.
- Move the `_discovery` field and its `StartAnnouncing` call out of `SspHostSession` constructor.
- Update `SspRoomTechnology.CreateAnnouncer(roomId, roomName)` to return `new SspRoomAnnouncer(...)` instead of `null`.
- `RoomFactory.Create` already calls `announcer.Start()` on non-null announcers — verify this wires up correctly.

#### Validation
- Build green.
- Room announcements (ANNC) still broadcast when hosting (verify via a second emulator or UDP listener).
- `SspHostSession` no longer owns discovery/announcing lifecycle.

---

### Slice 8 — Implement `ManualTask`
**Clash addressed:** #9

Architecture.md §4.2 specifies `ManualTask ..|> ITask` with `Fire(action)`.
Currently the host pad bypasses `ITaskOrchestrator` entirely and calls `room.FlashDeviceAsync` from `HostRoomPage`.

#### Scope
- Create `ManualTask : ITask` in `Application/Services/`.
  - `TargetKind Kind` — the output kind being controlled.
  - `Fire(FlashAction action)` — queues a single flash command through `room.FlashAsync(action, Kind)` or `room.FlashDeviceAsync(deviceId, action, Kind)`.
  - `StartAsync` keeps the task alive (awaits cancellation); `Stop` cancels it.
- Update `HostRoomPage` pad press/release to route through `ManualTask.Fire(action)` instead of calling `room.FlashDeviceAsync` directly.
- Wire `ManualTask` into the orchestrator so it coexists with Strobe/Auto mode switching (starting Strobe stops Manual, and vice versa where applicable).

#### Validation
- Build green.
- Pad press/release behavior identical to before (now routed through task).
- Mode switching between Off/On/Auto still works without conflicts.

---

### Slice 9 — Eliminate `LightController` singleton
**Clash addressed:** #13

After Slice 6 removes all `LightController` usage from tasks, this slice cleans up.

#### Scope
- Verify no remaining references to `LightController` exist.
- Delete `Luso/Light/LightController.cs`.
- Remove the `Luso/Light/` directory if empty.
- If any other consumer still uses it, inline the logic or replace with DI-friendly equivalent.

#### Validation
- Build green.
- No `LightController` references in codebase.

---

### Slice 10 — Architecture.md document refresh
**Clashes addressed:** #1, #2, #3, #4, #5, #6, #7, #8, #15

Batch update Architecture.md to align documentation with the actual (post-Slice 6–9) codebase:

| # | Section | Change |
|---|---|---|
| #1 | §4.1 class diagram | Rename `TargetKind.Screen` → `TargetKind.RgbLight` |
| #2 | §4.3 `IRoomHostSession` | Remove `StartAsync()`, `StopAsync()`, `CloseAsync()`, `OnGuestLatencyUpdated`. Add `DeviceCount`, `GetDevices()`, `IDisposable`. Document constructor-starts / Dispose-stops pattern. |
| #3 | §4.3 `IRoomGuestSession` | Remove `StartAsync()`, `StopAsync()`, `LeaveAsync()`, `OnFlashCommand`. Document that flash dispatch is internal to session impl. Add `IDisposable`. |
| #4 | §4.3 `IRoomTechnology` | Update signatures: `CreateHostSession(roomId, roomName)`, `CreateGuestSessionAsync(IDiscoveredRoom, LocalDevice)`, `CreateInviteSession(roomId, roomName)`, `CreateAnnouncer(roomId, roomName)`. |
| #5 | §5.1 sequence diagram | Remove `UI->>Room: StartAsync()` step. Note that `RoomFactory.Create` starts sessions/announcers inline. |
| #6 | §4.1 class diagram | Rename `IDevice.DeviceName` → `IDevice.DisplayName` |
| #7 | §4.1 class diagram | Change `ITarget.ExecuteAsync(FlashCommand)` → `ExecuteAsync(object command)`. Add note about cast-inside pattern. |
| #8 | §6 project structure | Replace `Discovery/`, `Contracts/`, `TaskSystem/` with actual layout: `Domain/Technologies/` for contracts, `Application/Services/` for tasks and discovery coordinator. |
| #15 | §6 project structure | Mark `Protocols/Hue/` as aspirational/future. |

#### Scope
- Text-only changes to `Architecture.md`.
- No code changes.

#### Validation
- Every interface, class name, method signature, and folder path mentioned in Architecture.md matches an existing code artifact.
- Class diagrams compile-check: every member shown exists in the corresponding `.cs` file.

---

### Slice 11 — Optional: Hue protocol skeleton
**Clash addressed:** #15 (aspirational)

- Add `Protocols/Hue/` directory with `HueRoomTechnology.cs` skeleton (invite-only, returns `null` for host/guest/announcer).
- No functional implementation required.
- Outcome: architecture proven for the invite-only device-provider pattern end-to-end.

---

## Operating rules
- Run one slice per PR/agent execution.
- Always validate against [acceptance-criteria.md](acceptance-criteria.md).
- Stop on unexpected cross-slice drift and report before continuing.
- Slices 6–9 are code changes; Slice 10 is doc-only; Slice 11 is optional.
- Slices 6 and 7 are independent and can run in parallel.
- Slice 8 depends on Slice 6 (ManualTask replaces the pad bypass fixed in Slice 6).
- Slice 9 depends on Slice 6 (LightController deletion requires usages removed first).
- Slice 10 should run after all code slices are complete.
