---
description: "Turn an Aquila roadmap item into a concrete implementation task"
name: "Roadmap Task"
argument-hint: "Roadmap item, issue number, or feature name (e.g. 2.5, #4, Storage sparklines)"
agent: "agent"
---

Work on the requested Aquila roadmap item: **$ARGUMENTS**.

Use these project references first:

- [Roadmap](../../docs/ROADMAP.md)
- [Project Context](../../docs/PROJECT_CONTEXT.md)
- [Workspace Instructions](../copilot-instructions.md)

## What to do

1. Find the matching roadmap item in `docs/ROADMAP.md` and restate the goal and constraints briefly.
2. Inspect the relevant files and existing patterns before proposing or making changes.
3. Follow Aquila's current architecture:
   - keep hardware polling and OS/LHM access in `Services/`
   - keep UI state and commands in `ViewModels/`
   - keep views/pages focused on XAML binding and injected `ViewModel`
   - reuse existing helpers, converters, controls, styles, and themes where possible
4. Avoid hardcoded machine-specific sensor identifiers in UI code; prefer `Helpers/SensorLocator.cs` and null-safe behavior.
5. If the request is ambiguous, ask one focused clarifying question. Otherwise, proceed with the smallest complete implementation or plan that fits the roadmap item.
6. Do not use long/noisy terminal commands in this repo, and never use `&&` in PowerShell. Prefer manual validation steps with `Ctrl+Shift+B` in Visual Studio.

## Response format

Use this structure:

### Target

- matched roadmap item
- current status/dependencies

### Approach

- short plan with the relevant files/symbols

### Result

- implementation details or a concrete next-step plan
- files changed or files that should be changed

### Verify

- concise manual verification steps for the user

### Follow-ups

- any related roadmap items, risks, or cleanup worth doing next
