# Agent Prompt — Refactor Slice 1

You are refactoring this repository in a single safe slice.

## Goal
Apply **Slice 1** from [docs/refactor-plan.md](../refactor-plan.md):
- Align project structure to Pages / Components / Core / Protocols
- Keep behavior unchanged
- Do not redesign protocol payloads or UX

## Mandatory constraints
- Follow [AGENT.md](../../AGENT.md)
- Follow [Architecture.md](../../Architecture.md)
- Keep changes minimal and scoped
- No unrelated cleanup

## Required validations
1. Build:
   `dotnet build /workspaces/SyncoStronbo/Luso/Luso.csproj -f net8.0-android -c Debug`
2. Check acceptance list in [docs/acceptance-criteria.md](../acceptance-criteria.md)

## Deliverable format
- What changed
- Why this matches architecture
- Validation output summary
- Risks/follow-ups
- Next recommended slice

## Stop conditions
- If the slice requires protocol redesign, stop and report blocker.
- If errors are unrelated to moved/touched files, report and do not broaden scope.
