# Agent Operating Rules (Luso)

This file defines mandatory rules for autonomous refactor agents working in this repository.

## 1) Mission
Refactor the codebase toward the architecture in [Architecture.md](Architecture.md) with minimal risk, small slices, and no behavior regressions unless explicitly requested.

## 2) Source of truth
1. [Architecture.md](Architecture.md)
2. [goals.md](goals.md)
3. [roadmap.md](roadmap.md)

If implementation conflicts with architecture, architecture wins unless a task explicitly says otherwise.

## 3) Non-negotiable boundaries
- `Core` must not depend on `Protocols`.
- `Protocols` may depend only on `Core` abstractions/contracts.
- `Pages` and `Components` must not import protocol-specific implementations.
- Keep protocol-specific wire details at protocol boundaries.
- No static global session/registry state unless explicitly approved.

## 4) Working style
- Refactor in small, isolated slices.
- Preserve runtime behavior by default.
- Prefer moving/renaming and adapter shims over broad rewrites.
- Do not change UX unless the slice requires it.

## 5) Forbidden changes (unless task says so)
- No protocol redesign in a structure-only slice.
- No broad UI redesign.
- No unrelated cleanup.
- No mass formatting changes.

## 6) Required validation per slice
- Build succeeds.
- No new dependency direction violations.
- Files moved to target structure compile with correct namespaces/usings.
- Existing key flows still work (create/join/invite/flash/kick/close).

## 7) Definition of done (per slice)
- Scope completed exactly.
- Build green.
- Delta summary produced.
- Open risks and deferred follow-ups listed.

## 8) Agent output format
- **What changed**
- **Why it matches architecture**
- **Validation run**
- **Risks / follow-ups**
- **Next suggested slice**
