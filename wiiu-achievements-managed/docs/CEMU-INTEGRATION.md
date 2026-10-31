# In-process Cemu integration — development fork

The owner-requested fork is based on official **v2.6**, commit
`a6fb0a48eb437a8a41c13b782ac8ae0433bf8f98`, rather than moving upstream main.
Repository: <https://github.com/hoffo1987/Cemu-WiiUAchievements>, branch
`codex/wiiu-achievements`.

This implementation does not certify the whole achievement set. The catalog
remains 70 executable objectives (53 experimental) and 40 unimplemented targets.
No new memory mapping has been declared verified.

## Running the development build

The Windows build and initial PC smoke checks have passed; see `VALIDATION.md`.
The current local executable is `build-check/cemu-integrated/Cemu.exe`.
Playable save/load/award and fullscreen-popup validation remain pending.

After successful CI, use the Windows artifact from the fork's **Cemu with Wii U
Achievements** manual workflow. Extract into its own directory. Keep
`WiiUAchievements/` beside the fork's `Cemu.exe`; it contains the managed module
and four catalog XML files. Cemu hosts .NET Framework v4 in-process and launches
no companion process. The original Desktop installation is not replaced.

Close the independent Wii U Achievements application first. Both use the same
per-user archive and existing exclusion mutex. Profile names, IDs, revision and
UTC unlock dates are preserved. A second writer is refused; the Tools menu
explains startup failures. The saved default profile/account/settings are reused.

Start the supported game. **Tools → Wii U Achievements** opens the existing
achievement interface on a separate STA inside Cemu. Closing that panel hides it
while recording continues. **Disattiva** stops recording; **Attiva obiettivi**
resumes with a fresh gameplay gate. **Pausa** affects achievement recording.
Cloud sync uses the existing optional account/client.

Configure the fork's game paths and existing MLC location normally. Do not put
games, keys, save files, logs or the achievement archive in the source repository.

## Synchronization contract

- GUI startup initializes the CLR and registers a rooted callback.
- The PPC `GX2SwapScanBuffers` HLE handler supplies copied observations at most
  once every 100 ms using `steady_clock`. Host VSync cannot manufacture samples.
- Native identification requires Title ID `0005000010143600`, version zero,
  and both base/loaded RPX hashes `cdc68acd`, obtained directly from Cemu.
- Title start/stop and debugger pause/resume advance a generation and publish
  reset boundaries. A capture mutex excludes title-memory teardown.
- Only guest stage/player/save ranges are read. The 26-byte stage and 16-byte
  player contexts guard both sides of the `0x724` save-window copy. Other PPC
  cores can still run: this is a guarded observation, not an atomic CPU stop.
- The callback copies a 1,952-byte versioned packet into a bounded mailbox.
  No disk, cloud, engine evaluation or window work runs on emulation threads.
  Lifecycle/context resets survive replacement of pending packets.
- The WinForms session consumes observations once. Duplicates, invalid
  fingerprints, time reversals, stale observations and pauses cannot advance
  confirmations. A gap over one second clears continuity.
- The same progression decoder, gameplay gate, engine, atomic progress store
  and cloud client are reused. The 750 ms gate and three confirmations remain.
  Existing-save recognition retains its production behavior.

Session sampling stays in `MainForm.cs`; `MainForm.Native.cs` owns the hosted
STA/window lifecycle, and presentation stays in `MainForm.Ui.cs`.
The native constructor uses its assembly directory for rules/resources, since
the default CLR AppDomain base directory belongs to Cemu. Standalone startup
retains its original directory and read-only external process reader.

Notifications currently use the existing layered Windows popup and generated
chime. An emulator-rendered overlay is not implemented; fullscreen visibility
requires testing. Cemu accounts/save slots do not automatically select local
achievement profiles. Exact-game mapping limitations remain in force.

## Build and tests

From this project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File integrations/cemu/prepare.ps1
# To refresh an existing committed fork already staged by this script:
powershell -NoProfile -ExecutionPolicy Bypass -File integrations/cemu/prepare.ps1 -UpdatePreparedFork
powershell -NoProfile -ExecutionPolicy Bypass -File integrations/cemu/build.ps1 -ManagedOnly
# Full build: VS 2022 C++ x64, Windows SDK, .NET Framework 4.8/4.8.1 SDK,
# CMake and the pinned upstream submodules are required.
powershell -NoProfile -ExecutionPolicy Bypass -File integrations/cemu/prepare.ps1 -InitializeSubmodules
powershell -NoProfile -ExecutionPolicy Bypass -File integrations/cemu/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-native.ps1
```

`prepare.ps1` requires the exact upstream base, accepts an identical already
applied hook patch and refuses conflicting edits. A hash manifest permits
updates only to its own previously staged files. `-UpdatePreparedFork` also
requires its staging manifest and ancestry from the pinned base; conflicting
local changes are still refused. The source/artwork allowlist
excludes user directories, binaries and raw game-memory data.

The manual workflow first compiles the actual native CLR host with a synthetic
memory fixture and runs it against the real managed assembly on a clean CI user.
Only then does it build full Cemu with pinned upstream submodules. It uploads a
development artifact, not a GitHub release. The native smoke script refuses
local/personal archives. Engine/UI/native-feed tests also run without a ROM.

See `VALIDATION.md` for actual results. Native source and the hook patch live in
`integrations/cemu/`. No EmuHook, injection, process-memory write API or game
patch is added. Cemu retains its upstream MPL 2.0 licence. This project and
owner-supplied artwork retain their existing licensing status.

## Required real-game validation

After a successful native build, check on this PC: exact title identification;
title/file-select exclusion; existing-save recognition; a new observed award;
area transitions; debugger pause/resume; end/relaunch title; close/reopen panel;
full Cemu restart with preserved dates; foreign/updated RPX rejection; and popup
visibility in the user's fullscreen mode. Check death/continue, new-game/hero
mode and different save slots separately. Experimental detectors still need
progressive playtesting; emulator hooks do not establish their game semantics.
