# Domain Glossary

## Room Provider
A protocol technology that can host/create a room and optionally announce it.

## Invite-only Device Provider
A protocol technology that never creates rooms, but can discover/manage candidate devices that can be invited into host orchestration.

## Target
A concrete controllable endpoint on a device (flashlight, screen, vibration, bulb).

## Task
A host-side control unit that emits commands over time for one `TargetKind`.

## Best-effort execution
Host dispatch is one-way. Guest execution success is not required as an ACK contract.

## Fresh join
Guest lifecycle starts from a new join. No resume/rejoin state machine in current scope.

## Identity tuple
Recommended device identity composition: `{name}:{protocol}:{protocolMeta}`.
If metadata changes, treat as a new device.
