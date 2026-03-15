# Refactor Acceptance Criteria

## Global architecture checks
- [ ] Target structure follows Pages / Components / Core / Protocols.
- [ ] Core systems are split by subsystem folders.
- [ ] No `Core` references to concrete protocol implementations.
- [ ] Protocol modules are self-contained.

## Build checks
- [ ] `dotnet build /workspaces/SyncoStronbo/Luso/Luso.csproj -f net8.0-android -c Debug` succeeds.

## Behavioral checks (current scope)
- [ ] Host can create room.
- [ ] Guest can discover/join room (fresh join).
- [ ] Host invite flow works.
- [ ] Host can trigger basic commands.
- [ ] Guest disconnect/host disconnect heartbeat behavior still works.

## Contract checks from Architecture.md
- [ ] Task cleanup only when a task has zero eligible targets.
- [ ] Identity metadata changes are treated as new devices.
- [ ] Host remains best-effort orchestrator (no required execution ACK).
- [ ] Security policy remains protocol-specific.

## Slice quality checks
- [ ] Only intended files changed.
- [ ] No unrelated refactors.
- [ ] Risks and follow-ups documented.
