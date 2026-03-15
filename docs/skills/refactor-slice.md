# Skill: Run a safe refactor slice

Use this skill whenever a task asks to migrate architecture or move project structure.

## Inputs
- Slice scope (folders/files/symbols)
- Constraints (behavior-preserving or behavior-changing)
- Architecture target ([Architecture.md](../../Architecture.md))

## Procedure
1. Confirm slice boundary and out-of-scope items.
2. Locate affected symbols and usages.
3. Apply smallest possible file moves/renames/signature updates.
4. Add temporary adapters only if required to keep compatibility.
5. Update DI registration/usings/namespaces.
6. Compile and resolve only slice-related errors.
7. Summarize changes + remaining debt.

## Safety checks
- No `Core -> Protocols` dependency introduced.
- No direct protocol imports from `Pages` or `Components`.
- No hidden global state introduced.
- No unrequested feature change.

## Validation checklist
- Build command passes.
- App critical flows unchanged for this slice.
- No unresolved compile errors in touched areas.

## Output template
- Scope completed
- Files changed
- Validation results
- Known limitations
- Next slice recommendation
