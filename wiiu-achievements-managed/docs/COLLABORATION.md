# Contributing with another AI

## Ready-to-paste prompt

```text
Work on the attached Wii U Achievements project. Read AGENTS.md and HANDOFF.md
first, then docs/ARCHITECTURE.md, docs/MEMORY-MAP.md, and docs/VALIDATION.md.
Preserve the independent implementation without EmuHook, read-only process
access, C# 5/.NET Framework x64 compatibility, and existing progress IDs.
Choose one bounded task from NEXT-STEPS.md and state its scope before editing.
Do not claim unobserved memory offsets or unrun live tests are verified.
Run the relevant checks, update the evidence/handoff, and return a patch or the
changed source files with build/test results and remaining uncertainties.
Do not include ROMs, keys, personal settings, progress files, or memory dumps.
```

## Reasonable independent tasks

- **Session/state work:** design explicit gameplay/load/save-slot validity, with
  fixtures for the log parser and a documented real-game validation plan.
- **Memory research:** investigate one item or state flag for the exact EUR
  build. Return candidate mappings and before/after evidence; keep unsupported
  guesses out of the enabled rule set.
- **Engine/tests:** add a narrowly specified condition type and regressions
  without changing memory mappings or existing achievement IDs.
- **UI:** improve the WinForms interface while preserving attach and progress
  semantics. Build to a separate directory if the user's app is open.

These are suggested contribution boundaries, not automatic authorization to
launch agents, contact people, or upload user data.

## Combining changes

Give each contributor the same clean archive and its SHA-256 hash, or the same
Git commit once the user has chosen to use a repository. Have each return source
changes or a patch, not just an unexplained binary. Keep one integrator responsible
for the combined build and checks. Avoid simultaneous edits to `Core.cs` and
`MainForm.cs` without agreeing on the interface first.

When contributions touch snapshot fields, agree on names, byte widths, validity,
and region/version scope before integrating. New XML rule fields need matching
loader and evaluator support. Preserve existing IDs and revision unless a
deliberate progress migration is included.

After integrating, run build/tests and record the actual manual checks. Generate
an archive only when explicitly requested; otherwise keep the root EXE current
and commit source/assets/docs. Never reuse an old manifest for changed code.

## Moving the work

Another AI can read and edit the project on any OS, but compiling/running this
WinForms/Win32 implementation and its integration fixture requires Windows x64.
Cloud Linux agents should return source patches and leave Windows/live-game
verification pending. The package does not contain a remote bridge to the user's
PC, and none is needed for code-only contributions.

For your own machine migration, copy `%LOCALAPPDATA%\WiiUAchievements\progress/` separately after closing
the app and keep the same profile name. Avoid sharing that folder with other AIs
unless you specifically want them to inspect personal achievement history.
