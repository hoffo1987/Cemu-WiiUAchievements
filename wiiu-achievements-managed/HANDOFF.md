# Current documentation entry point

**All remaining catalog detectors (2026-11-01):** all 40 formerly planned IDs
now have shared executable conditions: 110 active, 93 experimental, zero
unimplemented catalog targets. Read `docs/DETECTOR-EXPANSION.md` for sources,
the 44 distinct HD heart-piece sources, final-duel context, coverage and known
inferences. The owner has only the current save; no new late-game live
certification is claimed. IDs/revision/progress and native protocol are unchanged.
Earlier 70/53/40 counts below are historical and superseded.
513 core, 214 feed + 16 native UI, 145 standalone UI checks and the 110-ID
catalog/artwork audit pass. The 302-file source-only package rebuild passes.
The root EXE and the local integrated fork's managed module have been updated
while both apps were closed. No ZIP or personal-state migration was created.
The user explicitly approved publishing the fork and running CI after the
initial automatic-review rejection. Source commit `a7b03d0` is published on
`codex/wiiu-achievements`; CI run `41256997922` passed the real C++/CLR host
smoke and 214 feed + 16 native UI checks on a clean Windows runner. The full
Cemu EXE is unchanged; the local managed module and XML are current.

**Cemu integration attempt (2026-10-31):** the owner requested a fork with
in-process achievements. `integrations/cemu/` contains the pinned v2.6 native
CLR host, lifecycle/frame/menu patch and reproducible Windows build workflow.
`src/NativeCemuBridge.cs` and `MainForm.Native.cs` reuse the existing C# engine,
UI, archive and cloud client inside Cemu. See `docs/CEMU-INTEGRATION.md` and
the latest validation record. This does not certify the experimental detectors
or by itself validate the experimental targets. Keep the Desktop emulator separate until
native build and live validation are complete.

The Windows native build now passes (CI run `41240085236`). The usable local
fork is `build-check/cemu-integrated/Cemu.exe`, with its `WiiUAchievements/`
module beside it. Actual PC startup, same-process Tools panel, Wind Waker title/
menu waiting, clean exit and complete Cemu restart with the existing catalog
awards displayed passed. In-world save/load/awards and fullscreen popup checks
remain pending; see the dated validation record. The original Desktop EXE is
untouched. The fork was left open for the owner after these checks.

Current local executable: root `WiiUAchievements.exe`. Read `README.md` for the
current user guide, `docs/UI.md` for the final visual composition, and
`docs/VALIDATION.md` for dated evidence. The detailed handoff below includes
historical milestones; later progress recovery and automatic-load findings
supersede older event-only/manual-load statements.

Public images are generated with `tools/render-docs.ps1`, isolated synthetic
progress, no Cemu and no personal account. Never publish real user state.
The owner authorized pushing all current source/artwork/docs to GitHub `main`;
this does not authorize unrelated future external actions. No local ZIP is needed.

# Start here — handoff for another AI or developer

**Fixed window and performance (2026-10-31):** fixed 1120×840 logical window (no maximise), frame pacing in `FramePacing.cs`, cached video/parchment/cards and a frozen page under panels. See the latest `docs/VALIDATION.md` record.

**Integrated main (2026-10-31):** the UI redesign below and the complete
catalog/medal review further below are combined. Current UI facts are those of
the redesign entry and `docs/UI.md`: one toolbar with tabs, search, Categoria/
Tappa/Ordina and **Altri filtri** (rarity, detection, images); 132 px cards with
a 340 px minimum width. Statements in the review entries about a separate search
box, five direct menus or eight sort modes describe the intermediate version and
are superseded. All 110 medals, revised texts, audit and tools are current.
`build.ps1` uses a response file. 322 core and 143 UI checks pass.

**UI, graphics and motion redesign (2026-10-31):** presentation only. Toolbar
panel with icon tabs, search (Ctrl+F), Categoria/Tappa/Ordina menus (C/F/S still
cycle), five orderings, Azzera and a state key; cards read their state by icon,
word and outline; detail browser with ‹ ›; responsive banner; new catalog cards;
rarity-graded celebrations; reduced motion (Windows setting or saved choice).
Code is split into `MainForm.Toolbar/Cards/Detail.cs`, `Theme.Icons.cs` and
`Motion.cs`. 322 core and 133 UI checks passed before integration; see the latest `docs/VALIDATION.md`
record and `docs/UI.md`. IDs, revision, conditions and progress are unchanged.

**Complete catalog/editorial review (2026-10-31):** reviewed all 110 IDs
against conditions/planned requirements, rarity and icon subjects/frames.
Changed 60 titles and all 110 descriptions in `achievement-presentation.xml`;
all executable/companion rule files remain byte-identical. Native Italian names
and English search aliases coexist. Collection order now ascends by threshold;
initial minigames retain their real chapter. All 110 artwork frames match the
53 bronze / 47 silver / 10 gold assignments. Six icons were refined: song name,
sea sectors, visible bottles and the first barrel-game reward. There are now
58 deterministic compositions and 52 preserved owner PNGs. Cards are 132 px tall
with fully fitting current titles/descriptions; labels are 80×22 px, high contrast
and fully opaque for locked medals. 147 UI and 322 core/Win32 checks passed.
`docs/ACHIEVEMENT-AUDIT.md` records every ID and sources; the static audit is
`python -I tools/audit-achievement-catalog.py`. No detector was newly certified;
53 remain experimental and 40 unimplemented. IDs/revision/progress are unchanged.
The current executable remains in the project root; no ZIP or publication.
Historical counts and preservation claims below describe earlier milestones.

**Personalized medal artwork complete (2026-10-31):** all 110 current
objectives now have personalized icons. The owner requested public Wind Waker
PNGs/renders and explicitly chose precise composition without generative AI.
The 57 previously missing icons use the supplied rarity frames, proportionate
subjects, silhouette shadows and foreground boat/frame. All 53 earlier owner
runtime icons remain byte-identical. See `docs/ARTWORK-SOURCES.md` and the
hash-pinned `tools/artwork/wind-waker-hd/compositions.json`; the reproduction
tool is `tools/compose-achievement-icons.py`. Full 1254 px exports are local in
ignored `output/achievement-icons/`; only 256 px PNGs are embedded. The
**Immagini → Senza icona** filter remains and returns zero for this catalog.
The root EXE is updated; 133 isolated UI checks passed. IDs, revision, rules
and progress are unchanged. Historical counts below
refer to their dated milestones.

**Catalog browser and 17 more owner medals (2026-10-31):** 53 personalized
medals now replace matching glyphs; 57 remain. The "Artificiere del male" file
maps to existing "Artificiere del mare" without a title/ID change. Browsing now
has live keyword search, direct category/chapter/rarity/image/detection menus,
eight sort modes (including both rarity directions), result counts and reset.
The old missing-icon tab is now **Immagini → Senza icona**, combinable with
status and every filter. C/F open menus; Ctrl+F searches. See `docs/UI.md`.
131 isolated UI checks passed. IDs/revision/progress and engine semantics stay
unchanged; no detector was newly certified by this presentation change.

**Missing-art section (2026-10-31):** the fifth catalog tab, **Senza icona**,
lists objectives without personalized images (currently 74), including active
and pending targets. Counts follow category/chapter filters. This is UI only;
96 isolated UI checks passed. The root EXE is updated; IDs/revision/progress and
recording semantics are unchanged. See `docs/UI.md` and `docs/VALIDATION.md`.

**Additional owner medals (2026-10-31):** eight supplied images now replace the
glyphs for Il primo volo, Andata e ritorno, Fuoco e ghiaccio, La forza sotto la
lava, Piedi ben piantati, Riflessi d'eroe, Appigli da pirata and Vento in poppa.
There are 36 embedded owner icons. `prepare-game-art.py --icons-only` supports
additional batches without regenerating shared frames/sky. The root EXE is
updated; 91 isolated UI checks passed. IDs, revision and progress are unchanged.

**Owner clarification: retain nautical decoration (2026-10-30):** wind/water
motifs, the original parchment and the simple blue/gold frames are wanted.
Restored their visibility while keeping the revised logo positions and separate
banner status row. `SimpleFrame` copies proportional corner pieces and tiles
straight border strips; it does not stretch a whole illustrated panel. See the
latest `docs/UI.md` / `docs/VALIDATION.md`. Keep this balance in future UI work.

**Owner-requested composition review (2026-10-30):** the initial full-kit layout
was too busy. The follow-up removes unnecessary decorations/stretched sprite
panels, centres the project brand in a taller header, gives the Wind Waker logo
its own clear centred area (without the extra crest), moves status/live values
below the banner image, and uses one centred round badge in the catalog.
Parchment and shadows are quieter. See the latest `docs/UI.md` section and
`docs/VALIDATION.md` record. The prepared kit stays available, but use selected
ornaments with restraint; do not force every asset onto the screen. Current EXE
still belongs in the project root; no extra ZIP. This is presentation only.

**Full owner UI kit, logos and partial-paint fix (2026-10-30):** the root
`WiiUAchievements.exe` is the current build. The owner's full UI ZIP is prepared
into 40 game-only ornaments/sprites; the project logo and trophy icon live under
`assets/brand/`, and the two Wind Waker logos are game resources. See `docs/UI.md`.
The erased stripe through the chapter filter was a paint-culling bug: the tabs'
paint band stopped above the lower filter row. A pixel regression fails on the
old EXE and passes at 100/125/150/200 % rendering scale. Engine, memory, IDs,
revision and progress are unchanged; see the latest `docs/VALIDATION.md` record.
Owner preference: **keep the current EXE in the project root, and create no ZIP
or versioned output folder unless explicitly requested or necessary** (isolated
tests/running EXE). The disconnected old window was closed for this replacement.

**Ultrawide performance and owner UI art (2026-10-30):** scrolling at 3440×1440
went from 120 ms to 8 ms per frame (adaptive card cache, region invalidation,
cached layers, cheaper shadows/video); see `docs/UI.md` (Rendering performance)
and `docs/VALIDATION.md`. The owner's gold corners, Triforce crest and parchment
are in `assets/games/wind-waker-hd/ui/` and used by the Wind Waker style.

**Wind Waker theme and banner video (2026-10-30):** the objectives section of a
game now uses that game's full chrome (`GameStyle` palette + ornaments after the
medal frames); the Games catalog stays neutral. The banner plays the owner's Outset
Island video through Windows Media Foundation (`src/VideoBanner.cs`), with the drawn
sea as fallback. See `docs/UI.md` (Game styles, Banner video) and the latest
`docs/VALIDATION.md` record. The video is owner-supplied third-party footage. The owner explicitly requested
publishing the current source/assets/docs to GitHub main on 2026-10-30.

**Game styles and owner icons (2026-10-30):** the root `WiiUAchievements.exe` is
the current main build (progress recovery + this change); older builds were moved
to `build-check/archive/` (still scanned for legacy progress) and packages to
`release/`. The owner's 28 icons replace the matching medals; every other
objective draws an original glyph inside the same rarity frame. Each game has a
`GameStyle` (Wind Waker: sea palette, foam motif, popup panel); rarity drives
frame, ribbon, glow and chime. Unlocks animate as a grey-to-colour coin turn.
See `docs/UI.md` and the latest `docs/VALIDATION.md` record. IDs/points/revision
and progress are unchanged.

**Progress recovery (2026-10-30):** supersedes earlier no-retroactive-award policy
for real gameplay. Production now uses a shared per-user LocalApplicationData
archive instead of EXE-local `data/`, imports exact-identity legacy awards without
changing their dates, and shows saved unlocks before connecting. Real gameplay
recognizes already-satisfied conditions after the existing ready-context gate
and three confirmations; the legacy event-only mode remains for demo/regressions.
IDs/revision/points are unchanged. See `docs/PROGRESS-RECOVERY.md`; historical
sections below describe earlier behavior and must not override this change.


**Automatic loads (2026-10-30):** the exact Cemu 2.6 / EUR v0 / cdc68acd reader
now observes live-checked stage/transition/player signals, guards reads on both
sides of the save window, and resumes after 750 ms of coherent context. Supported
load flows no longer require manual pause/disconnect. A watcher reconnects the
same supported process after restart; explicit disconnect cancels it. Live
file-select/load, inventory/area and full-app restart tests passed with isolated
progress and no retrospective awards. See `docs/AUTOMATIC-LOADS.md` for evidence
and remaining cases; experimental achievement mappings are not re-certified.

**Firebase enabled (2026-10-30):** existing project `wiiu-achievements`,
email/password and Standard Native `(default)` in nam5/free tier. Private,
owner-only immutable unlock rules are deployed and reread from the server.
34 emulator checks and 9 real production C# client checks passed, including
upload/download, DPAPI restart/refresh and cross-user/anonymous isolation.
Six synthetic accounts and two documents were removed and verified absent.
`firestore.rules`, Firebase configs and the offline rule suite are included.
The account entry was clickable but unpainted; it is now drawn and covered by a
rendered-pixel regression. The user confirmed "Progressi sincronizzati" in their
own app. Connected sessions disable login/signup and account edits; logout
restores them. See `docs/CLOUD.md` for usage and prototype limits.

**Corrections (2026-10-30):** the broken account integration from `8b292ef` has
been repaired locally. Cloud operations are serialized background tasks; actual
unlock documents are pushed/pulled with durable retries from local progress.
Tokens use Windows DPAPI, renew on expiry/restart and leave no credential backup.
Account settings have a separate minimum-size page. See `docs/CLOUD.md` for
configuration, backend requirements and limits. The Firebase validation above
supersedes the earlier unprovisioned backend state. The automatic-load section
above supersedes the earlier manual-only recording boundary; regression counts alone still do not identify save slots.
The audit `docs/GROK-REVIEW-2026-10-29.md` describes the original broken commit,
not the repaired working tree. IDs/revision/personal progress remain unchanged.

This folder is the complete source project. No previous chat, account, cloud
service, hidden package, or original workspace path is needed to understand it.
The UI and user guide are Italian; technical handoff documents are English.

## Goal and current result

**Editorial rarity review (2026-10-29):** every target has explicit rarity in
`achievement-presentation.xml`: 53 bronze, 47 silver, 10 gold, zero current
platinum. Rarity is independent of points; platinum is reserved for a future
set-completion objective. Text/order corrections are presentation-only. See
`docs/RARITIES.md`. New isolated build: `build-check/rarity/`. IDs, points,
conditions, experimental flags and progress identity remain unchanged.


**Current development build — playthrough series (2026-10-28):** at the user's
explicit request to implement batches before they playtest them, there are now
70 executable rules, including **53 experimental detectors**, plus 40 still
unimplemented targets (110 total). See `docs/PLAYTHROUGH.md` for the chronological
groups. The new decoders are working code with synthetic coverage, not certified
HD mappings. The tunic and original threshold IDs/progress revision are retained.
`build-check/playthrough/` is the new isolated output; do not overwrite a live EXE.
The earlier sections below record the progression to this build.

The user wants a RetroAchievements-like system for Wii U games on PC, beginning
with **The Legend of Zelda: The Wind Waker HD** in **Cemu 2.6**. They explicitly
requested a fresh implementation, **not one built on EmuHook**.

The implementation is a Windows x64 WinForms application written in
C# 5, using .NET Framework and the Win32 read-only process APIs. It runs beside
Cemu. It evaluates 70 executable objectives (including 53 experimental detectors),
keeps 40 unimplemented targets separate, shows desktop notifications and
persists shared local progress with optional cloud sync. It is not integrated
with RetroAchievements accounts or servers.

**Catalog expansion (2026-10-28):** the current source has **110 objectives**:
16 executable rupee/heart thresholds (the original ten plus six additive rules),
one tunic acquisition rule and 93 story/equipment/exploration/collection/side-quest targets explicitly marked
pending validation. These 93 cannot unlock, even in demo mode, and do not enter
completion or point totals. Category/status filters and detailed validation notes
are built into the UI. IDs/revision and user progress are preserved. See
`docs/ACHIEVEMENTS.md` and the latest `docs/VALIDATION.md` record. The tunic
adds one targeted byte to production reads after a live before/after,
load, area/menu and handle-reopen check. Other inventory candidates stay research only.

**Scrolling fix (2026-10-28):** `MainForm.Tiles.cs` adds a bounded lazy bitmap
cache for static cards; scroll-only frames invalidate the grid, and introduction
delays depend only on viewport cards. Live progress and unlock animations remain
dynamic. See the latest validation record for timing and regression evidence.
The reviewed build is in `build-check/scroll-fix/`; the user's open earlier EXE
is not overwritten. The tunic was initially pending (the user reproduced its
absence of unlock); it now has an active detector at `0x145B81CE`, mask `0x80`.
It requires a stable absent baseline, so connect/resume BEFORE the delivery.
The user confirmed a real tunic notification in the corrected build; one saved
`story-heros-clothes` entry was verified locally. No manual award was made.
Loading saves still requires manual pause/disconnect; automatic gameplay and
save-slot detection remain unimplemented. Cemu restart coverage is pending.

**Alpha 0.2 (2026-10-28)** replaces the plain WinForms controls with a
custom-drawn, animated interface in the style of the Wii U system menus. It
also adds a layered, click-through unlock popup and a synthesised chime. Session,
engine, memory and persistence semantics are unchanged. The achievement IDs and
set revision are unchanged too. See `docs/UI.md`. This UI was built and
visually checked only on Linux with Mono (see `docs/VALIDATION.md`). **It had not
yet been run on Windows at that handoff.**

**Alpha 0.3 (2026-10-28)** adds a game catalog, exact profile/reader routing,
manual recording pause/resume, centralized per-rule evaluation status, stable
below-threshold arming and atomic batch unlock persistence. The ten original
rules, IDs, XML path, progress identity and revision are unchanged. No game
memory mappings or new achievements were added. See `docs/GAMES.md` and the
alpha 0.3 record in `docs/VALIDATION.md` for current evidence.

The alpha 0.3 code also passed Windows CI: official C# 5/.NET Framework x64 build,
50 core/Win32 checks, 22 UI checks, and a 47-file verified package. The workflow
artifacts contain a Windows-built EXE. This does not replace live Cemu tests on
the user's PC. See the linked run in `docs/VALIDATION.md`.

The first live unlock, **Primi spiccioli** at 10 rupees, was confirmed by the user
on 2026-10-28. It remained unlocked after closing and reopening this application.
That test did **not** restart Cemu. See `docs/VALIDATION.md` for the evidence and
the limits of what has been tested.

## Supported configuration

- OS/runtime: Windows x64, .NET Framework 4.x; the original alpha 0.1 was tested on the
  user's Windows PC. Later build evidence is recorded in `docs/VALIDATION.md`. Windows integration cannot run natively on Linux/macOS.
- Emulator: **Cemu 2.6**. The application deliberately rejects other versions.
- Game: **EUR v0**, Title ID **0005000010143600**, RPX hash **cdc68acd**.
- No game enhancements were enabled in the observed session; language was English.
- Host memory base is discovered per session, never hardcoded.
- Guest save header address is currently **hardcoded** to **0x145B7B80**.
  It was validated in one real Cemu session, not across a full playthrough or
  multiple real emulator restarts.

## Run, build, test

Open this folder in your editor. On Windows, run these commands from its root:

```powershell

# Existing binary: normal UI, explicit connection button
.\WiiUAchievements.exe

# Rebuild the current root EXE (close the disconnected application first)
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# Only when needed: test an isolated build without replacing a running EXE
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory .\build-check

# No Cemu or game required; includes a synthetic Win32 child-process fixture
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run.ps1

# Only on explicit request: build, test and create a shareable archive
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

Paths in scripts are resolved relative to the scripts, not the shell's working
directory. Paths containing spaces are supported. Build and tests use the x64
.NET Framework compiler under the current machine's Windows directory, not a
user-specific compiler path. No .NET SDK or package restore is required.

`-ExecutionPolicy Bypass` applies to that PowerShell process only; these commands
do not change the machine's persistent execution-policy configuration. If your
environment already permits local scripts, direct `./build.ps1` invocation works.

The UI's initial Cemu path is only a convenience guess under the current user's
Desktop. Select the real `Cemu.exe` on a different machine. The app also searches
for Cemu's log beside the selected executable and in the current user's
Roaming/Local AppData Cemu directories.

## Reading order and source map

1. `AGENTS.md`: project-wide guidance and invariants.
2. `docs/ARCHITECTURE.md`: control flow, types, persistence, and extension points.
3. `docs/MEMORY-MAP.md`: exact supported profile and evidence requirements.
4. `docs/VALIDATION.md`: what passed, what remains unverified, and live test steps.
5. `NEXT-STEPS.md`: ordered development priorities.
6. `docs/COLLABORATION.md`: contribution boundaries and integration procedure.

Source entry points:

- `src/Program.cs`: GUI entry point and single-instance guard per app folder.
- `src/GameCatalog.cs`: definitions, exact profiles and per-game memory adapters.
- `src/MainForm.Library.cs`: catalog rendering and navigation using the existing theme.
- `src/MainForm.cs`: attach sequence, 100 ms sampling timer, demo, settings, toast queue.
- `src/MainForm.Ui.cs`, `src/MainForm.Tiles.cs`: custom-drawn window, bounded card cache, layout, animations, input, accessibility.
- `src/MainForm.Toolbar.cs`, `src/MainForm.Cards.cs`, `src/MainForm.Detail.cs`: tabs/search/menus/order, card states, detail browser.
- `src/Theme.Icons.cs`, `src/Motion.cs`: state/navigation icons and the reduced-motion preference.
- `src/Theme.cs`, `src/Banner.cs`: palette, embedded Nunito font, vector icons/medals, sea banner.
- `src/Notification.cs`: layered unlock popup and synthesised chime.
- `src/GameSession.cs`: identification from the active Cemu log.
- `src/MemoryReader.cs`: process reader, guest-base discovery, calibration scanner.
- `src/Core.cs`: snapshot parsing, rules, achievement engine, XML progress store.
- `src/Gameplay.cs`: context decoding, automatic waiting and guarded continuity.
- `src/CloudSync.cs`, `src/CloudProject.cs`: background cloud client and public project defaults.
- `achievements.xml`: 16 thresholds in legacy-compatible format. `achievements-story.xml`: active tunic rule. `achievements-planned.xml`: 93 pending targets; `docs/ACHIEVEMENTS.md` explains promotion.
- `tests/CoreTests.cs`: executable regression suite, including the synthetic child.
- `tests/Probe.cs`: small optional live-memory diagnostic; not the app entry point.

## Recommended next task

Use the root `WiiUAchievements.exe` (rebuilt by `build.ps1`) as the current local build. The stage/load
reader and automatic reconnect are implemented for the exact profile; see
`docs/AUTOMATIC-LOADS.md` for real evidence and limits. Next verify death/continue,
new game/hero mode, different save slots and late-game transitions. Do not
reintroduce a generic multi-field regression counter as load identification.

Continue normal progressive playtesting of the 53 experimental detectors and
research the remaining 40 targets. Their status is independent of the new load
boundary and none was certified by these tests.

The existing Firebase backend is now enabled with the versioned private rules.
34 emulator authorization checks and 9 real production-client checks passed;
all synthetic accounts/documents were cleaned up and verified absent. Public
project defaults are prefilled. The account navigation entry is visibly painted.
The user confirmed their own app reports successful sync. See `docs/CLOUD.md`
before changing schema, catalog IDs or access, and rerun
`tests/test-firestore.ps1` for rule edits.

## What to request from the user for live work

Ask them to open the supported game in Cemu, describe the current game state,
and perform a precise action when needed. The absolute install path, current PID,
and host base are local session details, not prerequisites for understanding the
source. Do not request ROM uploads, console keys, passwords, or full memory dumps.

The portable engine suite runs with `bash tests/run.sh` on Mono. Windows
`tests/run.ps1` retains the native process fixture; `tests/run-ui.ps1` exercises
the UI in an isolated directory. GitHub Actions runs both Windows suites and
packages the application. Do not equate synthetic fixture/UI checks to Cemu tests.

For a remote AI without access to the PC, work on code, synthetic tests, and
documented hypotheses. Leave real-game validation explicitly pending.

## Portable handoff and personal state

Share the archive produced by `package.ps1`. It includes source, executable,
rules, documentation, tests, and a per-file SHA-256 manifest. It excludes `data/`,
personal settings, earned progress, logs, ROMs, and temporary artifacts.

To move **your own earned progress** to another PC, close the app and copy
`%LOCALAPPDATA%\WiiUAchievements\progress/` separately to the same user-data
location on the other PC. Re-select Cemu there; never copy `cloud.xml`.
Use the exact same local profile name; its trimmed value is part of progress
identity. Settings need not be copied. Renaming a profile, changing the game
fingerprint, or changing the set revision selects a different progress file.

Keep the app in a writable folder and keep `achievements.xml` beside its EXE.
The original user's data must remain intact during development.

## Licensing status

No redistribution license has been selected by the user yet. This project is
provided as an editable handoff for their collaboration. Do not invent an MIT,
GPL, or other grant, and do not imply that referencing Cemu or a trainer gives
this project that project's license. No external implementation is incorporated.
