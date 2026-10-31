# Validation record and reproduction

## Stuck "Attesa automatica" after leaving a house — 2026-10-31, Windows x64, live Cemu fork

Reported: after the Hero's Sword award and leaving the trainer's house on Outset,
the fork stayed in **Attesa automatica** and never resumed. A read-only probe on
the running Cemu (same `ProcessMemory` reader as the standalone app) showed the
stage buffer as `sea\0us\0\0` for both current and next scene: the game wrote the
shorter name over the previous, longer one without clearing the rest. The parser
required zeros after the terminator, so it returned Unknown on every frame. Bytes
after the terminator are now ignored; characters before it are still validated.
Checked live: the same running memory now parses as Ready `sea` (47 samples, no
context changes, no torn reads). 323 core checks (new leftover-name check; the
malformed-name check now uses an invalid character before the terminator),
205 feed + 16 native UI and 145 UI checks pass. The same parser is used by the
standalone app, which had the same defect. Not checked: resuming an award flow
in the fork with the fixed module (Cemu was running, its module was not replaced).

## Native status flicker during gameplay — 2026-10-31, Windows x64

Reported in the fork: during play the status kept switching between **Cemu ·
obiettivi attivi** and **Attesa automatica**. Cause: the native feed had a
one-slot mailbox and reset continuity whenever an unread packet was replaced.
Cemu submits at most every 100 ms and the UI timer also ticks every ~100 ms, so
two frames regularly arrived in one tick; each overwrite reset the engine and
restarted the 750 ms gameplay gate. A simulation (real `MainForm` in native mode,
a producer thread with 33 ms frames and the 100 ms cadence, stable gameplay)
showed 11 waiting entries in 20 s before the change and only the initial gate
afterwards. The feed is now a 16-packet FIFO drained in order at each tick;
resets stay attached to their packet and only an overflow drops data (marked
as a gap). An unread contradictory packet is now evaluated and breaks the
confirmation streak instead of being discarded. Checked: 205 feed + 16 native UI
checks (updated queue tests, overflow, two frames per tick), 322 core and 145 UI
checks. Not checked: inside a running Cemu with the game.

## Minimised window crash — 2026-10-31, Windows x64

Minimising the window (standalone app and the panel hosted in the Cemu fork) emptied
the client area; the resize handler then laid the cards out at zero size and the idle
card prewarm built a zero-width bitmap (`ArgumentException`, "Parametro non valido").
The exception left the message loop from `Application.Idle`, so the window was
destroyed; in the fork the message was then kept as a startup error and shown on
every **Tools → Wii U Achievements**. Layout now keeps the fixed window size while
minimised and the resize handler skips the empty state. Checked: a harness of the
fork's STA/`ApplicationContext` flow (minimise → reopen, close → reopen) reproduced
the error before and no longer fails; two new UI checks fail on the old code and
pass now; 322 core checks and 193 feed + 15 native UI checks pass. The existing
cloud UI check ("Account request runs in background…") fails identically with and
without this change. Not checked: inside a running Cemu with the game.

## Cemu in-process integration attempt — 2026-10-31, Windows x64

Implemented an official-v2.6 pinned fork overlay: in-process .NET Framework CLR
host, submitted-game-frame sampling (100 ms), exact Cemu title/RPX identification,
title/debugger lifecycle resets and Tools menu. The managed adapter reuses the
existing decoder/gate/engine/store/cloud/UI with bounded packet ownership and no
process-memory writer. See `CEMU-INTEGRATION.md` for scope and live checklist.

Local checked results: C# 5/.NET Framework x64 build succeeds; **322 core/Win32
checks**, **143 existing UI checks**, **193 native-feed checks** and **15
native-session UI checks** pass.
Native checks include torn context/menus, invalid identity, pause/generation
boundaries lost by queue replacement, stale/duplicate samples, exact decoder
parity, feed-to-engine atomic persistence, an unread contradictory sample being
dropped (which must reset confirmations), panel hide/recording behavior and
timestamp-preserving reactivation. All use isolated synthetic progress.
The default core-suite Temp attempt was denied at an atomic file move by the
sandbox; it passed with TEMP/TMP scoped to workspace staging, as in the earlier
validation record. No storage behavior was weakened.

Patch whitespace/reverse-apply and idempotent allowlisted staging passed.
The native host smoke test is CI-only and refuses an existing personal archive.
This PC has no MSVC/CMake toolchain; native compilation is delegated to the
fork's manual Windows CI. The native CLR smoke job in
[run 41239152369](https://github.com/hoffo1987/Cemu-WiiUAchievements/actions/runs/41239152369)
passed: real MSVC host compilation, CLR loading, hidden UI startup, file-select
and pause exclusion, stabilization, durable awards, stop/update rejection,
supported-title restart without duplicates and callback unregistration. This
is synthetic host memory, not a running Cemu game. The corrected mailbox also
passed the actual CLR smoke test plus all 208 feed/session checks in
[run 41239503388](https://github.com/hoffo1987/Cemu-WiiUAchievements/actions/runs/41239503388).
The first full-emulator build stopped before Cemu configuration because the
pinned vcpkg helper requested an MSYS2 pkgconf archive that all mirrors return
as 404. The workflow now selects an available native Windows pkg-config and
preserves it through vcpkg's documented environment override; Cemu, the library
baseline and ports are unchanged. Partial successful dependency packages are
cached even on build failure. Full build
[41240085236](https://github.com/hoffo1987/Cemu-WiiUAchievements/actions/runs/41240085236)
**succeeded**, including all upstream dependencies, the actual Cemu core/GUI,
CLR host and final Windows x64 executable. The native job took 22 minutes.
The development artifact was downloaded to `build-check/cemu-integrated/`;
its managed module was rebuilt from the current local source. Cemu-native
sources are identical to the successful CI commit `4737bd4`; subsequent source
changes only reorganize the managed session code and update documentation.

Real Windows PC smoke checks passed with that build: startup as Cemu 2.6;
Tools/Strumenti -> Wii U Achievements opens the existing catalog **inside the
same Cemu process**; closing/reopening the panel leaves Cemu running; the existing
recorded awards remain displayed. The configured Wind Waker HD launched through
the normal game list. Its log identified EUR v0 and both base/updated RPX
`cdc68acd`. The title screen/idle intro rendered in Vulkan; the native panel
received real observations and displayed automatic menu waiting without new
awards. Cemu then exited normally (exit code zero), and a complete new process
reopened the catalog with the existing recorded awards still present. The fork
is left open at the catalog/panel for the owner. An initial sandboxed launch
did not remain available; the actual UI check used an authorized desktop launch.

Not checked in this native fork: loading a save into playable gameplay, a new
in-world award, gameplay pause/load/death/slot transitions, sustained performance,
or popup visibility above a real fullscreen game. No experimental mapping is
certified, no ID/revision changed, and the Desktop Cemu executable was not replaced.

The handoff package allowlist now includes the native integration, adapter/tests
and the existing `FramePacing.cs` dependency. `package.ps1 -ValidateSourcesOnly
-SkipTests` verified the **301-file** staged manifest and rebuilt solely from
those staged sources. No ZIP was created; the earlier engine/UI/native test
results were not counted as packaging-time tests.

## Fixed window and performance pass — 2026-10-31, Windows x64

The window has one fixed size, 1120×840 logical pixels, and cannot be resized or
maximised; on screens too small for it at the desktop DPI the whole interface is
drawn at a smaller scale. Performance (synthetic benchmark, 1120×840, 144 Hz
display): full frame 14.9 → 4.7 ms; banner video frame 3.5 → 0.6 ms (frames are
composed at device size on the decoder thread); card hover 4.9 → 1.0 ms (cached
card plus lift/outline); settings panel animation ~30 → 6 ms (frozen page under
panels, parchment surfaces cached instead of a ~20 ms texture brush); layout
allocations 104 → 11 KB per frame (memoized catalog queries, per-card text).
Animations run frame by frame while no input waits, paced to the display refresh
(1 ms timer resolution only while animating); continuous animation measured a
steady 13.9 ms cadence (two refreshes at 144 Hz) instead of irregular 15–340 ms
gaps. Cards near the viewport are prepared while idle, so scrolling stays at
~1–6 ms. 322 core and 143 UI checks pass. Not yet explained: rare ~50 ms idle
pauses seen in the benchmark loop; not verified in a live Cemu session.

## Integration of the redesign with the 110-medal catalog review — 2026-10-31, Windows x64

The UI redesign (below) and the uncommitted work found in the main checkout (the
complete editorial/artwork review and its medals, recorded further below) were
combined on the owner's request. Kept from the review: all 110 personalized icons
(53 owner + 57 composed, with `docs/ARTWORK-SOURCES.md`; the owner approved
publishing them), the revised titles/descriptions/chapters in
`achievement-presentation.xml`, the audit and composition tools, the 132 px card
height and the high-contrast rarity labels. Kept from the redesign: toolbar,
menus, cards, detail browser, catalog and motion. The review's separate search
box, context menus and eight-way sort were replaced by the redesign's toolbar;
its rarity, detection and image filters became one grouped **Altri filtri (R)**
menu, and its Italian/English search aliases now feed the redesign's search.
Rules, IDs, revision, conditions and progress are unchanged.

Also fixed: `build.ps1` passes compiler arguments through a response file, since
110 embedded medals in a deep folder exceeded the Windows command-line limit;
`docs/ACHIEVEMENTS-DISPONIBILI.txt` (stale titles and pre-recovery recording
advice) is regenerated from the loaded catalog by `tools/render-docs.ps1`, and
the identical root copy was removed.

Checked: build succeeds from the worktree path; `tests/run.ps1` **322 checks
passed**; `tests/run-ui.ps1` **143 checks passed**, including every title,
description and requirement fitting the narrowest card (340 px, the new minimum
card width) and the detail card, the grouped filter menu, rarity/detection/image
combinations, empty "Senza icona" result and English/boss search aliases;
`python -I tools/audit-achievement-catalog.py` passed (110 IDs, texts, images,
frame rarities, unchanged rules). Scroll timing: median 1.9 ms, p95 18 ms.
Screenshots and the list were regenerated synthetically.

## UI, graphics and motion redesign — 2026-10-31, Windows x64

Presentation-only rework of the Wii U chrome and the Wind Waker section; see
[UI](UI.md). New: toolbar panel with icon tabs, accent-insensitive search
(Ctrl+F), Categoria/Tappa/Ordina menus with counts, five orderings, Azzera and a
state key; cards with medal well, adaptive titles and state shown by badge icon,
chip words and outline (check/lock/flask/hourglass, dashed for not implemented);
detail browser with ‹ › navigation and a facts table; responsive banner height
with status disc and shoreline seam; redesigned Games catalog; rarity-graded
celebrations and chime; reduced-motion mode following Windows or a saved choice.
Engine, memory reader, sampling, conditions, IDs, points, revision and progress
format are unchanged; only `settings.xml` may gain an optional `<motion>` element.

Checked on this Windows x64 machine (synthetic data only; no Cemu, ROM, real
progress or account):

- Official C# 5 / .NET Framework x64 build (`build.ps1`) succeeds, in Temp and
  in this worktree's root. The owner's running EXE in the main checkout was not
  touched.
- `tests/run.ps1`: **322 core/Win32 checks passed** (unchanged code paths).
- `tests/run-ui.ps1`: **133 UI checks passed in three consecutive runs** before integration
  (91 before). New checks cover search (case/accents, counts, empty state and
  reset), every ordering, Azzera, menus (separate input layer, all entries,
  keyboard focus on the current entry, arrows/Enter, Esc returning focus, click
  outside), accessibility roles for drop-downs, detail browsing bounds and keys,
  distinct state words, toolbar/banner/grid stacking without overlap and card
  width ≥ 320 px at 900×680, 1120×800, 1920×1080 and 2560×1080, reduced motion
  (settings without slide, no grid entrance, popup medal still turns grey to
  colour, saved `<motion>` choice and its removal), and partial repaints of
  tabs, menus, search and state key matching whole frames at 100/125/150/200 %
  (0 differing pixels).
- Scroll timing (110 cards, 80 grid frames, same machine): median 1.7–2.4 ms,
  p95 21–23 ms, versus 1.6 ms / 20.5 ms for the previous commit run alongside.
- Visual review of the synthetic frames from `tools/render-docs.ps1` at
  900×680, 1120×800, 1920×1080 and 3440×1440 (ultrawide, halved), plus menus
  opening below and (at minimum size) above, search, empty search, hover, detail
  at minimum size, card celebration frames, popups per rarity and catalog.
  Long titles wrap instead of being cut; secondary state words are only drawn
  when they fit whole.

Test-suite note: the previous commit's UI suite also failed intermittently on
this machine in the cloud section (1 of 2 runs), and always when run from a very
long output folder. Causes: an automatic sync could still be in flight when the
controls were asserted, and `cloud.xml.<guid>.tmp` exceeded the Windows path
limit. The test now waits until account work is idle and reports the account
state on failure; run it from a short folder (the `%TEMP%` default).

Not verified: live Cemu sessions, the layered popup over a real fullscreen game,
real Windows display-scaling changes (only offscreen 100–200 % rendering), the
"Show animations" system switch toggled at the OS level, and screen readers.
Hover and press were checked through rendered frames and input simulation, not
by a person using the mouse. Achievement mappings are not re-certified.

## Complete 110-objective editorial/artwork audit — 2026-10-31, Windows x64

Reviewed every effective title, description, executable/planned requirement,
rarity, category/chapter/order and personalized image. Updated 60 titles and
all 110 descriptions in editorial metadata; all three rule XML files remain
byte-identical, as checked against the pre-review snapshot and recorded hashes.
IDs, revision, thresholds, points, categories, experimental flags and persistence
identity are unchanged. Native Italian terminology is based on Nintendo's Italian
manual/page and archived in-game name citations; English aliases stay searchable.
See `docs/ACHIEVEMENT-AUDIT.md` for every ID, findings and source links.

Clarified balance versus cumulative earnings, maximum versus current health,
capacity versus current ammunition, filled bottles, initial chart sectors and
compound boss/sword/soup conditions. Clarified the next 20 Portafelicita after
the villa, HD rewards, 14 exterior decoration pedestals, three deciphered Triforce
maps and Mingle's HD gallery requirement. Collection tiers now ascend in gameplay
order; initial minigames stay with their actual chapter. The current 53 bronze /
47 silver / 10 gold assignments were retained after review. A static pixel check
of 15,367 opaque outer-ring samples confirms the correct frame for all 110 PNGs;
the two initially suspected small-preview mismatches were already correct.

Six images were improved: Canzone del Vento replaces incorrect baked-in wording
with Zephos artwork; three sea-chart icons use the Fishman instead of a special
chart; four bottles are separately visible; the first barrel-game reward is a
Piece of Heart rather than the second-win treasure map. The deterministic
manifest now covers 58 compositions and 52 unchanged earlier owner PNGs, retains
the superseded owner's hash and adds a rule-file contract. No generative editing
or game/model archive extraction. The import tool accepts both planned/current
editorial titles and legacy source titles.

Rarity labels are 80x22 logical px with 13.64 px heavy dark lettering and a flat
light metal face. They keep full opacity under locked medals; the same renderer
serves cards, details and notifications. Nominal palette contrast is 8.78:1 bronze,
10.35:1 silver, 8.94:1 gold and 9.71:1 platinum. Cards are 132 px tall, support two
title lines and three description lines; the compact banner is 176 px so a full
expanded row still fits at 900x680. Static/dynamic progress footers use the same
new location. These choices were visually inspected in full and minimum-size
previews, including labels at 1x and 2x.

Checked: **147 isolated UI checks passed**, including the measured fitting of
all 110 titles and descriptions at the narrowest card text widths and in the
detail dialog, contrast and untrimmed labels, local/English search aliases,
filter/menu/sort/cache behavior, 100/125/150/200 percent partial-repaint and search
font checks, recording and offline progress. An initial search assertion expected
only the explicit boss tags, overlooking ordinary text matches such as Octoboss
and descriptions mentioning bosses; it now checks that battle IDs are included
and unrelated dungeon equipment is excluded. A later rerun exposed stale fixture
award dates from reusing the output folder; the startup-progress fixture now has
a unique per-run directory. No production persistence or search matching rule
was weakened to satisfy either assertion. Final scroll sample: median 1.48 ms,
p95 16.20 ms across 80 grid frames.

**322 core/Win32 checks passed**. The default Temp-staged attempt was denied at
an atomic file move by the sandbox; the identical suite passed with TEMP/TMP
scoped only to workspace test staging. No storage code changed. The static
catalog audit passed all 110 IDs/text/image/rarity mappings, preserved owner
hashes, unchanged rule hashes and ascending collection groups. All 110 embedded
PNG streams in the rebuilt root EXE match final asset SHA-256 hashes.

Regenerated all 18 public documentation PNGs from isolated synthetic state.
Closed only the observed disconnected root app, rebuilt its root EXE and reopened
it. The new real window shows revised titles/labels and the existing four saved
unlocks. No live Cemu test, new detector certification, ZIP or publication. The
53 experimental and 40 unimplemented targets retain their existing status.

## Complete personalized medal artwork — 2026-10-31, Windows x64

Filled the 57 missing personalized icons (20 bronze, 28 silver, 9 gold) using
publicly available Wind Waker PNGs and the owner's supplied rarity templates.
The owner explicitly selected precise compositing without generative AI.
Promotional Nintendo artwork, official item renders and published original-model
PNG renders are individually distinguished in `docs/ARTWORK-SOURCES.md` and the
hash-pinned composition manifest. No game/model archive was downloaded or ripped.
Proportionate subjects use two silhouette shadows behind them and the original
boat/frame in front. Collection thresholds have small original number badges.

Checked: all 57 full exports are 1254×1254 RGBA with transparent outer corners;
all 57 runtime exports are 256×256 RGBA; source/template/export hashes validate;
all 53 previously supplied runtime icons remain byte-identical. All 57 ID/rarity
mappings match current editorial metadata. Inspected all three labelled contact
sheets and the updated rarity-order app preview. All 110 embedded PNG streams
in the isolated compiled app match the prepared runtime asset SHA-256 hashes.

The C# 5 / .NET Framework x64 build and **133 isolated UI checks passed**.
The suite checks all 110 embedded icons, the now-empty missing-art filter,
combined status/category/chapter filters, and continued missing-art support for
a synthetic future game without personalized resources. Search, rarity sorting,
menus, caches, recording and offline progress checks remain passing. The first
run failed the focused Escape assertion; the harness now explicitly restores
the search focus after its expensive preview render before dispatching Escape.
The complete rerun passed. No production keyboard behavior was changed.

Regenerated all 18 documentation PNGs with isolated synthetic state in workspace
staging. The existing disconnected root app was closed, the root EXE rebuilt,
and the app reopened. The real window retained its four saved unlocks. Selected
**Immagini → Senza icona** through its real menu: zero of 110 results, as expected;
then reset the catalog. Existing progress, IDs, revision, points, rules, memory
and persistence semantics are unchanged. No new Cemu or core-engine validation,
ZIP or publication was performed. The package allowlist now includes the source
register, deterministic composition tool and owner templates for reproducibility.

## Search, direct filters, sorting and 17 owner medals — 2026-10-31, Windows x64

Added 17 prepared 256×256 RGBA images (53 supplied icons, 57 fallbacks total).
The filename "Artificiere del male" aliases the existing "Artificiere del mare"
(`gear-bombs-99`). Shared rarity frames/sky, rule titles, IDs and set revision
are unchanged. The owner supplied all source images; no artwork was generated.

Replaced category/chapter cycling with direct menus, added rarity/image/detection
filters, eight sort choices, live text search with accent/case/punctuation
normalization and equipment keywords, result/filter counts and reset. The
missing-art view is now **Immagini → Senza icona**, independently combinable with
status. A shorter proportional banner at minimum height leaves room for the
new controls and a full card row; normal-height composition is retained.

Checked: C# 5 / .NET Framework x64 build; **131 UI checks passed** using isolated
synthetic state. Coverage includes title/description/keyword search, native
search focus and typing shortcut isolation, all sort modes, direct menu action,
filter combinations and empty states, 53 embedded icons, 57 missing-art targets,
minimum-size nonoverlap, overlay visibility, scrolling/caches, partial filter
repaints and native search-font scaling at 100/125/150/200 % offscreen scale,
recording and offline progress. The preceding 124-check run's scroll harness
measured median 1.34 ms, p95 18.63 ms across 80 bitmap grid frames.
Visually inspected filtered search results and the 900×680 browser preview.

An earlier complete run passed 123 checks. Follow-up runs exposed a timing race
in the existing synthetic cloud UI checks: login immediately starts a separate
sync, temporarily disabling logout. The harness now pumps the continuation and
waits for that operation before asserting control states; production cloud
logic is unchanged. The 124-check rerun and final 128-check DPI run passed.

The subsequent real-window menu test found that disposing a ContextMenuStrip
in its Closed event runs before WinForms dispatches the item's click, causing an
ObjectDisposedException. Menus now stay alive until the next opening or form
disposal. Three regressions exercise closure-before-click and reopening; the
final **131-check** suite passed. The affected root app was disconnected.
Rebuilt and reopened the corrected root EXE, selected "Rarità: più rare prima"
through its real menu without an exception, focused search with Ctrl+F, typed
"capacita bombe 99" and observed the single matching Artificiere del mare card
with the supplied icon. Escape cleared the query while retaining rarity order.
The existing four saved unlocks remained displayed. All 53 embedded PNG streams
were also checked against the prepared asset SHA-256 hashes.

The documentation renderer regenerated 18 public PNGs from isolated synthetic
state, adding search, rarity order and missing-art examples. The default Temp
staging attempt hit a sandbox atomic-move denial; the same renderer completed
with staging and synthetic state inside the workspace test-results directory.

No new core-engine or live Cemu test; no memory, persistence schema, achievement
conditions, points or detector certification changed. No ZIP or publication.

## Missing personalized artwork section — 2026-10-31, Windows x64

Added the fifth catalog tab **Senza icona**, using the actual per-game embedded
icon availability. It lists all 74 current fallback medals, including executable
and pending targets, independently of unlock status. Its count follows category
and chapter filters; accessible help explains that personalized images are missing.

Checked: C# 5 / .NET Framework x64 build; **96 UI checks passed** with isolated
synthetic data. Added coverage for the 74-target membership/count, exclusion of
supplied art, category/chapter combinations and an empty selection, browsing
during recording without replacing the engine or changing unlocks, and the fifth
tab fitting at 900 px. Existing partial-repaint checks still pass at
100/125/150/200 % offscreen scale. Visually inspected the 900×680 filtered preview.
Closed only the observed disconnected root app before rebuilding its EXE.
No new live Cemu or core-engine test; no engine/memory/persistence/ID/revision change.

## Eight additional owner medal images — 2026-10-31, Windows x64

Matched the owner's PNG filenames to the existing titles/IDs for Il primo volo,
Andata e ritorno, Fuoco e ghiaccio, La forza sotto la lava, Piedi ben piantati,
Riflessi d'eroe, Appigli da pirata and Vento in poppa. Prepared eight 256×256 RGBA
icons with premultiplied-alpha resampling, retaining the supplied complete frame.
The new `--icons-only` preparation option leaves shared frames and sky intact.
The existing renderer uses these images in cards, details and notifications.

Checked: official C# 5 / .NET Framework x64 build; **91 UI checks passed** with
isolated synthetic data, including 36 embedded owner icons, matching existing
IDs, cached rendering, locked/colour medals, popups and offline saved progress.
The root EXE was rebuilt after the old disconnected window was closed. No ZIP
was created. All eight embedded PNG streams match the prepared asset files by
SHA-256. Reopened the root application and checked its normal disconnected
window, with the existing four saved unlocks still displayed.
No engine, memory, persistence, ID, revision or detector changes;
no new live Cemu validation or core-engine test was performed.

## Documentation and GitHub refresh — 2026-10-30, Windows x64

README and operational guides now describe the current 70 executable / 53
experimental / 40 unimplemented catalog, shared progress storage, automatic
loads and existing-save recognition. Dated historical validation entries are
preserved as evidence, not presented as current operating instructions.
All 15 tracked screenshot PNGs are regenerated by `tools/render-docs.ps1`, using
only an isolated synthetic profile, fixed example award dates, a generic Cemu
path and a synthetic activity log. No real profile, credentials or Cemu process
is used. The harness covers main/catalog/settings/detail/locked detail/growth/
drawer/paused/unlock, three popup rarities, popup timeline and both medal sheets.

Checked: the documentation renderer compiles against the official Windows x64
build and completes; the core/Win32 suite was rerun: **322 checks passed**.
The current production UI already passed **91 checks** in the retained-theme
record below. Markdown local links and staged file scope are checked before
publication. The owner explicitly requested publication on GitHub `main`.
No local ZIP or new release directory is created; CI package artifacts remain
confined to the runner's temporary directory.

## Retained wind, water, parchment and simple frames — 2026-10-30, Windows x64

Owner clarification restores the liked nautical motifs and the simple blue/gold
frames inside the revised logo/status layout. Confirmed that the attached
512×512 parchment matches the existing embedded source pixel-for-pixel. Texture
opacity is increased on bars, cards, status, settings and detail; winds return
to the sky, waves to the footer and card corners. `SimpleFrame` uses uniformly
scaled corners and repeated straight border strips with a separately tiled
parchment centre. No complete illustrated panel or logo is stretched.

Checked: C# 5 / .NET Framework x64 build; **91 UI checks passed**, including
partial filter repaint equality at 100/125/150/200 %, cached card/layer reuse,
minimum-size navigation/settings, offline saved progress and synthetic cloud
controls. Visually inspected the rendered main view, settings and detail panel.
Scroll harness across the two renders: median 1.57–2.22 ms, p95 20.38–22.40 ms across 80 bitmap grid frames at
1120×800. No new live Cemu or core-engine test was performed; no engine, memory,
persistence, ID or revision change. Current build remains the root EXE, no ZIP.

## Owner-requested composition review — 2026-10-30, Windows x64

The full-kit placement was rejected as too busy. Presentation now reserves a
centred header area for the project logo, a clear centred left banner area for
the Wind Waker wordmark (without the extra crest), and one centred round badge
for its catalog card. Status/version/live values occupy a separate row below
the image. Removed stretched illustrated frames/rules, repeated card waves and
most backdrop ornaments; lighter parchment, borders and shadows.

Checked:

- Official C# 5 / .NET Framework x64 build; current root EXE updated without a ZIP.
- `tests/run-ui.ps1`: **91 checks passed** on the final source, including partial
  filter repaints at 100/125/150/200 %, card/layer cache reuse, catalog navigation,
  minimum-size settings, offline progress and the synthetic account fixture.
- Rendered and visually inspected: 1120×800 main view, catalog, detail, settings,
  demo values; 900×680 minimum view; 3440×1440 ultrawide; 200 % offscreen scale.
- Additional offscreen 900×680 render with synthetic 5000 rupees, 79/80 quarter
  hearts (`19¾/20`) verified that the three value columns fit without overlap.
  The extra no-message-loop renders exercised the drawn-sea fallback; the main
  UI suite decoded the embedded video and rendered its regular banner as well.
- Reopened the root EXE and visually checked the real Windows window, including
  the video, centred project wordmark, clear game-logo area and retained saved
  progress view. No emulator connection was started.
- Bitmap scroll harness on the final source: median 1.59 ms, p95 20.21 ms for
  80 grid frames at 1120×800. These timings include newly entering cards and do
  not measure physical screen refresh.

Only presentation files changed. No new live Cemu test, engine/memory/persistence
change, new detector certification, ID/revision migration or cloud schema change.
The earlier 322 core checks remain the previous record, not a new run here.

## Full UI kit, project/game logos and filter stripe — 2026-10-30, Windows x64

Inputs: owner's full UI ZIP (18 PNGs), project logo with trophy, round Wind Waker
badge and Wind Waker wordmark. All ZIP images were visually inspected, including
both sprite sheets and the reference preview. `tools/prepare-ui-art.py` prepares
40 game UI PNGs, two game logos, two shared brand PNGs and the multi-size ICO.
The reference preview and duplicate atlas elements are not displayed as sheets.

Checked:

- Official C# 5 / .NET Framework x64 build; PNGs embedded and project trophy used
  as the Win32 EXE icon. Current deliverable is the root `WiiUAchievements.exe`.
- `tests/run.ps1`: **322 checks passed**, including the read-only native child
  fixture, engine, progress recovery and synthetic cloud client. The sandboxed
  attempt denied atomic file operations; the full Windows runs passed outside it.
- `tests/run-ui.ps1`: **91 UI checks passed**, with isolated synthetic data.
  New checks cover all 40 UI resources, both game logos, project branding and
  refusal to draw Wind Waker ornaments/frames through the neutral style.
  One additional rerun stopped with a null detail after a synthetic click while
  desktop UI interaction was also being exercised; the next fresh isolated run
  completed all 91 checks. No production exception was observed.
- The narrow repaint regression fails on the previous EXE (5,987 changed pixels
  at 100 %) and passes on the corrected renderer (zero differing interior pixels)
  at 100 %, 125 %, 150 % and 200 % logical rendering scale. It covers both filter
  buttons and their adjacent help text. This is offscreen render scaling, not a
  claim that Windows display scaling was changed during the test.
- Rendered and visually inspected: main window, 200 % rendering, Games catalog,
  settings, detail panel and popup; minimum-size navigation remains in the suite.
  Scrolling harness across passing runs: median 1.84–2.55 ms, p95 21.73–24.23 ms for 80 bitmap grid frames at
  1120×800 (includes first builds of entering cards; no monitor refresh claim).

The root app was reopened and visually checked, including wheel scrolling and
return to the first objectives; the filter row stayed intact and saved progress
was still visible. Sprite silhouettes were checked individually and isolated
to remove neighbouring fragments from the atlas crops.

No live Cemu detector, memory, engine, revision, point or progress changes.
The old app was observed disconnected and closed through its window before the
root EXE replacement. No new ZIP was made. Temporary test builds were needed for
isolated checks; no new versioned build folder is part of the deliverable.

## Ultrawide scrolling performance and owner UI art — 2026-10-30, Windows x64

Owner report: scrolling the objectives lagged badly fullscreen on an ultrawide
screen. This PC's primary monitor is 3440×1440 at 100 %. A harness rendered the
form at that size (paint work only; then into a WinForms `BufferedGraphics` DIB
like a real WM_PAINT).

| 3440×1440 | before | after |
| --- | --- | --- |
| scroll frame (grid band) | 120 ms, ~90 card rebuilds/frame | 8.0 ms in the DIB buffer (12 ms into a plain bitmap), 0 rebuilds |
| full window | 147 ms | 24 ms |
| banner | 19.7 ms | 7.3 ms (video 3.9 ms) |
| top / bottom bar | 1.5 / 1.7 ms (15 / 17 ms with the parchment before caching) | 1.0 / 1.3 ms |

At 1120×800: scroll 1.5 ms, full window 8 ms (was 14 ms). Root causes: card cache
capped at 48 with ~70 cards in view; whole-window repaints on hover; large
shadows filled under their content; banner repainted on every 15 ms tick.

Owner UI art (`wind_waker_ui_assets.zip`, 6 PNGs) cropped and embedded; texture
verified seamless (edge difference 0). Checked: `tests/run.ps1` **322 checks
passed**; `tests/run-ui.ps1` **84 UI checks passed**, including new checks that
the card cache budget covers twice the cards in view, repainting a scrolled view
rebuilds no card, and bars/tab track/banner overlay are reused between frames.
Rendered and inspected: main window, growth category with a gold earned card,
detail dialog, settings, popup.

Not checked: the real screen refresh while scrolling with a mouse wheel (the
harness measures paint work), 150–200 % DPI, and the popup over Cemu.

## Wind Waker themed section and banner video — 2026-10-30, Windows x64

Input: the owner's `windwaker-wallpaper-ytmp4.savetube.vip.mp4` (640×360, 30 fps,
H.264 + AAC, 317.7 s). Measured with OpenCV: one continuous day cycle with ~1–2 s
cross-fades near 78 s, 159 s and 243 s; looping the whole file is natural. Embedded
without audio (4.06 MB).

Checked:

- Media Foundation interop decoded real frames on this PC: first frame shown while
  paused, 91–92 frames in 3 s of playback, pause holds the frame, orientation correct,
  seek to the local time of day after skipping to the key frame target (16:04 → day).
- `tests/run.ps1`: **322 checks passed**. `tests/run-ui.ps1` in fresh folders:
  **81 UI checks passed**, including: banner video embedded; Wind Waker style themed
  and neutral style not; objectives use the game style and the Games catalog the
  neutral one; the banner decodes a frame or falls back (here: decoded).
- Scroll benchmark after caching the wave bands: median 2.0–2.4 ms, p95 14–16 ms
  (previous build 1.9 / 13.6 ms); before caching it was 5.3 / 20.5 ms.
- Rendered and inspected: main window at 1120×800, settings sheet, activity drawer,
  unlocked and locked detail dialogs, Games catalog (neutral, with the video card),
  paused state, popup frames. One early preview run threw inside `Render` and could
  not be reproduced in eight further runs; the banner video draw is now guarded and
  falls back to the drawn sea for that frame.

Not checked: real playback over hours, behaviour on Windows N/Server (expected
fallback), 150–200 % DPI, and the popup over a running Cemu game. No engine, memory,
persistence, ID, point or revision change.

## Owner icons, rarity frames, game styles and folder layout — 2026-10-30, Windows x64

Inputs: the owner's `icone.zip` (28 RGBA PNGs, 1254 px). Each file name matched an
achievement title exactly, except "Uno sguardo al mare" → `gear-picto-box`
("Uno sguardo sul mare"), mapped by an explicit alias. Measured with NumPy: all 28
share one frame template (alpha mismatch 0.2–0.4 %); the frame metal already
matches the curated rarity of every icon (18 bronze, 9 silver, 1 gold).
`tools/prepare-game-art.py` wrote 28 icons, four frames and the sky (3.8 MB total).

Checked:

- Build with the official C# 5 / .NET Framework x64 compiler; all PNGs embedded.
- `tests/run.ps1`: **322 checks passed**, including the new legacy-source check
  for builds moved to `build-check/archive/`.
- `tests/run-ui.ps1` in fresh folders: **77 UI checks passed**, including the new
  art checks: 28 icons embedded under existing IDs; every frame and the sky
  present; every Wind Waker objective has its icon or an explicit glyph; owner
  icons replace exactly 28 objectives; frame rarity equals curated rarity; locked
  medals are muted versions; different objectives draw different items; medal
  cache reuse and bound (≤ 160); popup medal turns from grey to colour; one chime
  per rarity, longer for rarer medals. A second run in the *same* output folder
  stops at the existing cloud check because account state from the first run is
  reused there; this is unrelated to the art and was not changed.
- Rendered and inspected: all 110 medals unlocked/locked (`--preview-medals`),
  main window with synthetic unlocks at 1120×800 and 900×680, three celebration
  frames, unlocked/locked detail dialogs, popup frames from 0.3 s to 3.2 s for
  bronze, silver and gold, and 2× popups. Images are in `docs/screenshots/`.

Not checked: the popup over a running Cemu game, sounds by ear, 150–200 % DPI on a
real monitor, and the reorganised folder with the user's own data beyond the
automatic import rules. IDs, points, revision `wwhd-alpha-1`, conditions,
experimental flags and progress identity are unchanged; no award was made.

## Shared progress recovery and existing-save recognition — 2026-10-30

Root cause confirmed by comparing exact-identity local progress files: the
active build had only one entry, while earlier playthrough/rarity builds retained
the tunic and telescope with their original timestamps. No deletion or lost
memory offset caused those two missing awards. EXE-relative archives had split
progress between build folders. The 100-rupee objective was absent from all those
archives; its condition was still present in the running game.

`UserData.cs` now resolves one per-Windows-user LocalApplicationData archive.
Normal releases share that archive and an archive-keyed mutex. Legacy imports
read only the same profile/title/version/RPX/revision filename and validate the
embedded identity, merge allowed missing IDs atomically and retain existing dates.
Source files and credential backups are not rewritten. Existing shared account
and settings files are never replaced. Explicit test roots skip legacy discovery;
previews are isolated. No game files, credentials, dumps or personal progress are
included in the source/package.

Production uses `IncludeExistingSave`; the two-argument engine constructor keeps
legacy `NewEventsOnly` for demos and older regression callers. The production
policy requires a real ready context, the existing 750 ms gate and three valid
met samples. Pending targets never execute. Recorded IDs never re-award; initial
recognition gets the observation timestamp and a distinct notification, rather
than an invented original acquisition time. IDs, revision and points are unchanged.
Threshold descriptions now match this policy. Experimental mappings retain their
experimental status; this change does not certify the 53 detectors.

**321 core/cloud/native checks passed**, including 23 new recovery/recognition
checks: exact-identity union, date preservation, idempotence, unchanged donors,
unknown-ID filtering, different profiles/revisions, spoofed identity, reopening,
menu/unknown/contextless suppression, stable loaded recognition, missing-condition
reset, duplicate prevention and post-reconnect dates. Existing event-only tests
are retained as checks of that explicit legacy mode.
**64 UI checks passed**, including offline startup display/count/date preservation
and isolated data roots. Scroll median 3.62 ms / p95 9.44 ms over 80 frames.

Live check on the existing Cemu 2.6 / EUR v0 / cdc68acd session: updated the runtime
from a separately built/tested candidate after closing only the achievement app.
Four legacy local files were verified unchanged by SHA256 during installation.
The normal app migrated into the real user's shared LocalApplicationData archive,
showed three stored awards before connecting and preserved both historical story
timestamps exactly. After connecting read-only to the already loaded game with
137 rupees, it automatically recorded `wallet-100`. The resulting archive contains
four awards, including all three requested objectives; the visible app shows
4/70 and 16 points. No manual Award or memory writes were used for this repair.
The app was then closed and reopened normally: the shared progress file SHA256
was unchanged and all four records remained. A read-only, exact UID/profile
Firestore API check confirmed the app had uploaded all four IDs with matching
recorded timestamps (within Firestore's documented precision). No admin writes
were used to repair the user's cloud data; the production sync worker uploaded it.



## Firebase deployment and visible account entry — 2026-10-30

The user explicitly authorized fixing the existing backend so they can try sync.
Published `firestore.rules` to `wiiu-achievements` with Firebase CLI after a
successful compile/dry-run. Rereading the deployed rules confirmed the owner
checks and executable ID whitelist; SHA256 of the validated local source:
`BA6E169E77823D7A65B0D74A85AD459355DD8804D5F3D3E13F64887CE6361722`.
The plugin's first deployment job reported success without changing the rules;
that no-op was detected by rereading, then the CLI deployment actually released
them. No billing, provider or database provisioning changes were needed.

**34 Firestore emulator checks passed** with the final repository config/runner,
Node built-ins and Java 25, demo project and loopback only. Coverage includes the
exact desktop create-only PATCH/paginated list, ownership/anonymous isolation,
immutable records, strict field types/key whitelist, time limits, unknown/planned
IDs, hash format, parent role escalation, nested paths, collection-group reads
and overwrite preconditions. The server whitelist equals all 70 executable IDs.
`tests/test-firestore.ps1` runs these separately from game-free .NET tests.

**9 real C# client checks passed** with the production `CloudSync` implementation:
temporary email/password registration/login, upload/readback, import into a
second empty local store, DPAPI persistence and token refresh after constructing
a fresh client, cross-user get/list denial, anonymous denial and empty independent
account. Six accounts and two synthetic documents across harness iterations were
removed by their recorded exact IDs; each account lookup and collection list was
verified empty afterward. No user's achievements or credentials were changed.
A first workspace-local harness failed a local File.Replace before creating any
account; isolated Temp roots passed. Harness identity construction and timestamp
precision assertions were corrected before the successful run, without changing
production sync. Firestore rounds below microsecond precision; local originals
are retained and remote round-trip checks account for that precision.

The user's UI trial exposed an account entry that was present in the input map
but never painted by DrawSheet. A normal restart reproduced it, ruling out a
stale process. Added the missing DrawButton call and a rendered-pixel regression
before clicking the entry. The corrected entry and account page were then opened
in the visible app with Cemu connected and the original unlock retained.
**61 UI checks passed**, including this regression,
minimum-size account controls, password masking and background account requests.
Scroll median 2.13 ms / p95 6.52 ms over 80 frames. Final runtime binary is built
in `build-check/firebase-final/`; existing settings and progress were copied
without overwrite and verified by SHA256. The user then confirmed
"Progressi sincronizzati" in their own app. This is separate from the nine
network probes that used synthetic accounts. Their follow-up exposed confusing
login buttons remaining enabled after successful authentication: connected
sessions now disable login/signup and account edits, retain logout and explicitly
say the account is connected. A repeated handler invocation also preserves the
existing session. Logout restores editable controls; startup recognizes saved
authentication without asking for another login.

**298 core/cloud/native checks passed** during packaging. The 71-file manifest
was verified and the staged source rebuilt independently. Packaging first failed
under the sandbox Temp ACL at atomic file move; rerunning with the normal user
permissions passed without a production code change. The package includes public
Firebase configs/rules and the offline runner, excludes test accounts/manifests,
credentials and all personal state. Negative schema tests use a valid executable
ID so the schema condition, rather than an unknown ID, is exercised.

The rules are a prototype for private progress isolation, not proof of legitimate
gameplay. Review before broad distribution. No achievement ID/revision, engine,
reader, persistence or memory access behavior changed in this follow-up.
Historical inspection below records the deny-all backend before this deployment.



## Automatic load boundaries and Firebase account inspection — 2026-10-30

Current supported-profile recording no longer requires manual pause/disconnect
for the observed loading flows. `Gameplay.cs` adds stage/next-stage/player
context and a 750 ms stability gate; the production reader observes context
before and after save bytes. Title/file-select, unknown memory and uncertain
actor replacement clear transient conditions. Observed normal in-world scene
requests preserve arming but discard confirmation streaks; permanent regression
still resets. A watcher reopens only the same exact supported Cemu executable,
profile and validated guest reservation after loss. Explicit disconnect cancels
pending reconnect/attach continuations. No guest address is used as a host address.

Live read-only evidence on Cemu 2.6 / EUR v0 / cdc68acd:
- File selection `Name`, transition request 0→1→0, `sea`, absent→matching Link
  references; repeated after a game restart and read-only handle reopen.
- New production reader/engine: automatic Menu→Loading→stabilizing→Ready with
  15-rupee loaded state and zero retrospective awards in an isolated test store.
- User opened/closed inventory and entered a house; `sea`→Loading→`Omasao` and
  automatic recording resumed with zero retrospective awards.
- Complete MainForm, offscreen/muted/isolated profile: unavailable session→auto
  reconnect→title/file-select→loaded `sea`; no intervention in the achievement
  app and zero retrospective awards. The title's `sea_T` background contains a
  player-like actor: it was explicitly excluded after this observation, with a
  decoder regression based on the observed stage. The final title exclusion was
  not separately repeated live. Final preservation of in-area arming and attach
  cancellation were checked synthetically, not by an actual boss acquisition.

**298 core/cloud/Win32 checks passed**, including 26 new gameplay-boundary checks:
load-forward without permanent drops, menu headers, title actors, stabilization,
invalid names/pointers, area continuity, unknown/gap resets, no retrospective
awards and a legitimate crossing after resume. The native child fixture now
provides separate context pages and verifies the production reader through real
ReadProcessMemory. **56 UI checks passed** in the final automatic-load run, including automatic
waiting, reconnect controls and explicit cancellation of a pending attach.
Scroll median 2.43 ms / p95 6.43 ms over 80 frames. Packaging verifies a 66-file
manifest and recompiles only the staged sources; the final archive is also
extracted and rebuilt independently before delivery.

Firebase read-only inspection found existing project `wiiu-achievements` and its
web app. Authentication email/password is enabled. `(default)` is Standard,
FIRESTORE_NATIVE, nam5, free tier. Deployed Firestore rules deny all reads/writes;
no backend rules, accounts or billing were changed, and no actual unlock transfer
was performed. Public client configuration is prefilled in `CloudProject.cs`.
Private tokens, settings, progress and CLI logs stay out of Git and packages.

See [AUTOMATIC-LOADS.md](AUTOMATIC-LOADS.md) for exact fields/evidence/remaining
cases, and [CLOUD.md](CLOUD.md) for current backend requirements. New-game/hero,
two different slots, death/continue, late-game transitions and a full playthrough
remain untested live. Polling is not frame-perfect; experimental achievement
mappings have not been promoted by this work. Older manual-only notes below are
historical and do not describe this build.


## Corrections after the Grok commit audit — 2026-10-30, Windows x64

Application builds with the official .NET Framework compiler, C# 5/x64. Changes:
- Fixed the premature `MainForm` class closure; removed the redundant multi-field
  reset and restored mandatory manual pause guidance. The engine also detects
  maximum-heart rollback; full gameplay/load/save-slot detection remains pending.
- Account settings use a separate page that fits 900 x 680. Cloud fields are
  editable during recording, password fallback drawing is masked, and leaving
  the page or starting auth clears the password.
- Auth and synchronization run in serialized background tasks, never synchronously
  inside the sampling loop. The UI stays responsive during a pending test request.
- Real per-ID UTC unlock documents, paginated pull, immutable remote creates,
  local atomic validated import, profile/game/revision isolation, and durable
  retries from local progress replace the previous timestamp-only skeleton.
- Framework JSON parser/serializer, token expiry/refresh/restart, DPAPI-protected
  credentials and legacy-backup removal are implemented. Local progress format,
  IDs, set revision, points, memory reader and offsets remain unchanged.
- Package allowlist includes cloud source/tests/setup documentation; packaging
  now recompiles only its staged sources to catch omissions.

**272 core/cloud/Win32 checks passed**, including 24 new checks for import atomicity,
unknown IDs, timestamps/reload, defensive snapshots, profile identity, heart
regression, formatted JSON/escaping, encrypted storage, renewal/restart,
offline retry, logout/project switches, migration and pagination. Native Win32
fixture uses only synthetic child memory. Cloud tests use an injected fake REST
transport, not an external account. DPAPI checks are explicitly Windows-only.
**53 UI checks passed** in the final isolated run, including 8 new cloud
regressions. A repeated visible-window run failed its pause-click assertion;
the test now positions its window offscreen to isolate native desktop input,
while retaining synthetic mouse/keyboard dispatch and actual rendering. Final
pause/resume checks pass. Scroll median 2.46 ms / p95 6.22 ms over 80 frames.
Minimum-size account preview visually inspected on Windows.
The earlier diagnostic report describes the original commit, not these fixes.

Package creation passed the 62-file manifest and independent staged-source
rebuild. Existing EXEs and personal progress were not replaced or copied into the
new output. No real Firebase project, security rules deployment, multi-device
cloud transfer or Cemu live session was tested. Configure and validate the backend
as described in [CLOUD.md](CLOUD.md); these synthetic results do not certify it.

## Independent audit of commit 8b292ef — 2026-10-29, Windows x64

Local `main` was fast-forwarded from `4329afa` to remote `8b292ef`. The received
application **does not compile**: `MainForm.cs` has account methods outside its
class (`CS0116`/`CS1022`). No existing executable or personal progress was changed.
The original source passed **248 core/Win32 checks**, which do not exercise
the new cloud code. All **45 existing UI checks** passed only in an isolated
diagnostic copy with the premature class-closing brace removed. That one-brace
change was not applied to the main working copy.

Twenty synthetic diagnostic observations reproduced load-detection limitations,
invalid JSON handling, token/backup issues, UI overlap and read-only account
fields, and clear-text password rendering during the settings animation. A
1200 ms loopback HTTP delay blocked WinForms timer delivery for 1225–1235 ms. No
Firebase account/project or Cemu process was contacted. The diagnostic package
passed its 58-file manifest with `-SkipTests`, but rebuilding its extracted
source failed because `CloudSync.cs` is absent from the package allowlist.

Full scope, evidence, severity and limitations: [audit report](GROK-REVIEW-2026-10-29.md).
The older passing-build records below do not certify this commit. Manual
Pause/Disconnect before loading saves is still required: the new multi-field
counter is a subset of the engine's existing regression guard, not a complete
gameplay/load/save-slot detector. Cloud unlock synchronization is unimplemented.

## GitHub main synchronization checks — 2026-10-29, Windows x64

- Refreshed `origin/main`; local `main` and remote shared the same starting
  commit before committing the accumulated development changes.
- Official C# 5/.NET Framework x64 build passed into
  `build-check/github-sync/`, leaving existing application executables intact.
- **248 core/Win32 checks passed** and **45 UI checks passed**. UI artifacts
  remain local in `build-check/github-sync-ui/`; scroll traversal median was
  **2.13 ms**, p95 **6.93 ms** over 80 frames.
- The first sandboxed core run stopped on temporary-file access; rerunning with
  the required filesystem access passed the complete suite.
- `git diff --check` passed. Only source, rules, tests and project documentation
  were included; no local progress, settings, build outputs or game files were
  added. No live Cemu validation or new package validation was performed.

## Editorial rarity and ordering review — 2026-10-29, Windows x64

- All 110 catalog entries have explicit rarity: **53 bronze, 47 silver, 10 gold**;
  no current platinum. The future set-completion platinum is reserved separately.
- Rarity no longer derives from points. The editorial file cannot change fields,
  thresholds, points, IDs or experimental flags. Missing entries, unknown IDs,
  duplicate IDs, unsupported attributes, unknown rarities and inappropriate
  platinum assignment fail loading before game selection changes.
- **248 core/Win32 checks passed**: existing progression/engine/native/persistence
  regressions plus rarity coverage, equal-point/different-rarity cases, story
  ordering and malformed editorial metadata. Legacy progress fixture still loads.
- **45 UI checks passed** including curated medal rendering, chronology, filters,
  cached live footers/unlock invalidation, demo/pause/resume and minimum size.
  The scroll pixel test now locates its wallet card after the deliberate numeric
  ordering change. Traversal median **2.15 ms**, p95 **6.33 ms** over 80 frames.
- Inspected Windows main preview: readable rarity names below medals; existing
  layout, art and experimental markers preserved. Detail pills and notifications
  share the same rarity rather than a separate points-derived classification.
- Separate build in `build-check/rarity/`; previous live executable left running.
  Settings/progress copied locally to missing destination files only, never into
  the shared package. Original points, stable IDs, `wwhd-alpha-1`, all conditions
  and memory offsets remain unchanged. No new Cemu evidence is claimed.

See `RARITIES.md` for the editorial criteria and corrected ordering/descriptions.

## Implemented playthrough series — 2026-10-28, Windows x64

The user explicitly requested implementation in batches before they verify the
events during a normal playthrough. This development build implements **53 new
experimental automatic rules**, for **70 executable objectives / 40 unimplemented
targets** (110 total). It does not certify the new mappings as live-verified.

- Official C# 5/.NET Framework x64 build succeeds into `build-check/playthrough/`.
- **241 core/Win32 checks passed**. Every executable addition is checked for no
  retrospective award on a completed initial state, a stable live crossing,
  persistence reload and atomic simultaneous awards. Additional decoder tests
  cover cumulative upgrades, sparse masks/popcounts, song identities, distinct
  pearls, invalid/unavailable encodings, capacities independent of current
  inventory, bottle contents, 49 mapped sectors, exact boss stages plus per-quest
  conjunctions, permanent-state rollback and defensive continuity snapshots.
- A reconstructed, sequential playthrough passes the actual parser, engine and
  progress store through clothing, telescope, sword, shield, pirates, first
  fortress, first dungeon tools, pearls, Tower of the Gods, Master Sword,
  Helmaroc, Zelda, elemental upgrades, temples, Triforce and light arrows. Each
  stage checks the exact award IDs and absence of unrelated awards. This is
  synthetic evidence, not a real completed playthrough.
- The Win32 child fixture now commits a synthetic two-page save window at the
  supported guest offset; the production reader reads it through real
  `ReadProcessMemory`, verifies the host=base+guest translation and decodes
  header, clothing and inventory. The four previous native scanner checks pass.
- **44 UI checks passed**: chronological default ordering, active experimental
  story cards, combined chapter/category/status filters, F chapter navigation,
  explicit pending versus executable labels, prior demo/pause/settings/profile
  behavior, bounded cache and no stale live footers. Inspected full-size and
  900 x 680 Windows previews. Scroll traversal median **2.32 ms**, p95 **6.18 ms**
  over 80 grid frames including cache fills/eviction, preserving the earlier fix.
- **Passive real-session baseline only:** exact Cemu 2.6 / EUR v0 / `cdc68acd`,
  current log and guest reservation matched. Without asking the user to perform
  actions, the 50 decoder fields were all available: relevant initial item,
  song, boss and event values zero; wallet capacity 500; three mapped sectors;
  zero bottles; clothing bit set. No raw state buffer/log was saved. This
  baseline does not validate acquisition or late-game mappings.

Read-only access, original IDs, `wwhd-alpha-1`, earned timestamps and existing
progress identity are preserved. Source-derived experiments retain explicit
labels in cards, details, accessibility, log and notifications. The demo does
not invent these fields, so absent data cannot arm them. A rollback in persistent
values resets transient conditions, but **automatic gameplay/load/save-slot
detection is still incomplete**: manual pause/disconnect before a load remains
required. Boss flags may persist on leaving/warping out of the boss area.

Live acquisitions for these 53 rules, per-field menu/load/reconnect/restart
checks, late-game HD layout and a full playthrough remain pending. Earlier real
rupee/tunic evidence below remains separate. See `PLAYTHROUGH.md` for the groups
the user can now verify naturally, without repeating an acquisition ritual.

## Combined scrolling/tunic build — 2026-10-28, Windows x64

The catalog now has **17 active objectives** (16 thresholds plus the green tunic)
and **93 pending targets**, with unchanged IDs and `wwhd-alpha-1` progress identity.

- **80 core/Win32 checks passed**. New checks cover the clothing bit mask,
  stable acquisition and persistence, no award for an already-received tunic,
  unknown data not arming the rule, unknown data clearing a pending acquisition,
  rejection of non-boolean snapshots and impossible boolean thresholds.
- **41 UI checks passed**, including the scrolling/cache regressions and an
  active tunic entry in the Story/Da sbloccare filters. Pending entries explicitly
  say Non attivo and announce that automatic unlocks are unavailable.
- The full-catalog 80-frame grid traversal in this combined build measured
  median **2.73 ms**, p95 **7.14 ms** (same 1120 x 800 setup as below; cold entries
  and cache eviction included). These are synthetic render timings, not a Cemu
  frame-rate measurement.
- Real targeted map observations: the user's acquired state read `0x80`; loading
  the earlier save before delivery read `0`; receiving the tunic again read
  `0x80`; leaving the house and opening the game menu still read `0x80`.
  Each observation used a newly opened read-only handle, exact profile matching
  and a reservation/log cross-check. See `MEMORY-MAP.md` for the derivation and
  remaining limits. No other story/inventory field was enabled.
- Built into `build-check/scroll-fix/` without stopping the user's previous app
  or Cemu. Settings and saved progress were copied locally from the running
  earlier build's data directory, only to missing destination files. The shared
  archive excludes this personal data.
- Packaging rebuilt from source, reran all 80 core/Win32 checks and verified a
  **53-file manifest**, including the card-cache source and active story XML.
- **Live end-to-end tunic unlock confirmed by the user.** The user launched the
  corrected build, loaded the pre-delivery save with recording disconnected,
  connected before delivery, waited for the baseline and received the tunic.
  They confirmed the notification **Il colore dell'avventura** appeared. A
  subsequent read of the local progress XML confirmed exactly one saved
  `story-heros-clothes` entry. No manual or retrospective award was written.

The tunic reads one extra byte at guest `0x145B81CE`, decoded with mask `0x80`.
The snapshot represents absent data separately from a real zero, including in
demo mode. The 16 original/additive thresholds still read the same six-byte
header. The active story rule lives in `achievements-story.xml`; the original
threshold file remains readable by the old executable. All three catalog files
are copied by build/test scripts and validated for revision/ID consistency.

Still pending: real application restart recovery of this specific tunic unlock,
Cemu restart stability and automatic gameplay/save-slot detection. **Pause or
disconnect before loading saves and connect/resume before the delivery.** The
app deliberately does not award a tunic already present on connection.

## Achievement scrolling performance — 2026-10-28, Windows x64

User reported heavy scrolling lag in the expanded build. A same-machine rendering
probe on the 110-card list (1120 x 800, 96 DPI, .NET Framework x64, no live game
attachment) measured 80 timed frames after 16 warm-up frames:

- Before: full-frame median 23.70 ms / p95 28.61 ms; grid median 19.14 ms /
  p95 21.40 ms. Layout alone was 0.06 ms median.
- After bounded static-card caching: full-frame median 12.27 ms / p95 12.62 ms;
  warm grid median 1.69 ms / p95 1.80 ms.
- The committed UI suite also traversed the entire catalog over 80 grid frames,
  including new cache entries and eviction: median 2.24 ms / p95 5.90 ms.
  These are rendering times, not measured screen refresh rates or a guarantee
  about Cemu load, other DPI settings or every PC.
- **40 UI checks passed**, including eight scrolling/cache regressions: reuse
  within a row, live progress pixels changing without rebuilding static cards,
  unlock medal invalidation, bounded cache during long traversal, access to the
  final target, release of cached entries and intro visibility deep in the list.
  A further accessibility check confirms that pending story targets explicitly
  announce the absence of automatic unlocks. Existing catalog, filters,
  pause/resume, demo, profile, minimum-size and settings checks still pass.
- Inspected Windows-rendered pending catalog preview: original layout, text,
  medals and shadows preserved. No engine, memory or progress format changed.

Static card images are limited to 48 entries and cleared on resize, game changes
and disposal. Scroll-only animation frames no longer repaint the banner/top bar.
The previous open executable and all personal progress remain untouched. Build
the fix separately with `build.ps1 -OutputDirectory .\build-check\scroll-fix`.

The user also reported receiving the green tunic again after loading an earlier
save, with no achievement. This is expected for its current **pending** catalog
entry: no clothing field or acquisition condition exists in the production
reader. This report is not evidence that the tunic detector works. A candidate
event bit was subsequently observed set once; live transition validation and
safe session/load detection are still required. Pending cards now say **Non
attivo** explicitly, and their accessibility description states that automatic
unlocks are unavailable.

## Expanded catalog verification — 2026-10-28, Windows x64

Checked on the original user's Windows PC while the existing application and
Cemu remained open. The running root EXE was not overwritten or stopped.

- Official C# 5/.NET Framework x64 build succeeded into `build-check/`.
- **73 core/Win32 checks passed**. Added coverage verifies all original IDs,
  thresholds and points, 16 executable versus 94 pending entries, strict pending
  metadata validation, category validation, unique IDs across both entry types,
  no pending awards even with maximum synthetic values or a pre-existing stored
  pending ID, and stable crossings/no retrospective awards/persisted reload for
  each of the six new thresholds. The synthetic native process fixture passed.
  The runtime threshold XML remains compatible with the older executable;
  duplicate IDs across companion files and executable companion rules are rejected.
- **31 UI checks passed**, including the 22 prior checks plus active-only
  completion totals, pending/category filtering, category keyboard cycling,
  pending accessibility explanations, detail cards, rendering while sampling and
  all four tabs fitting at the minimum width. Tests used an isolated directory,
  synthetic data and muted audio; they did not attach to Cemu.
- Visually inspected Windows-rendered pending-grid and pending-detail previews at
  900 x 680. Category controls, orange pending labels, proposal points, explanation
  text, status tabs and the active-only ring fit. Full-size and live-demo pending
  previews were also generated. Existing sea artwork, font license and sound
  sources were preserved.
- `package.ps1 -DestinationPath .\build-check\WiiUAchievements-expanded.zip`
  rebuilt, reran all 73 checks and verified a **51-file** manifest. New research
  source and catalog documentation are explicitly allowlisted. No local user
  data is included. This package is an expanded alpha, not a certified game set.
- Manual targeted read-only initial-item probe matched Cemu 2.6 / EUR v0 /
  `cdc68acd`, its log and its guest reservation. The user reported having received
  no initial items. Known header: 12/12 quarter hearts, 17 rupees. Telescope,
  sword and shield candidate bytes each read zero once. This is absence-only
  baseline evidence, not a verified acquisition detector. See `MEMORY-MAP.md`.
- The user clarified that the green tunic was already acquired. It is now the
  first of 26 story targets, before the telescope. No clothing flag was read or
  validated and no retrospective achievement was granted from this report.

The six new executable thresholds use the unchanged header reader; their real
game crossings have **not** been tested. All 94 story and extra targets remain
explicitly pending and cannot unlock. No new production memory offsets, gameplay/
save-slot detection, live inventory acquisition, menu/load/reconnect/restart
validation, full playthrough, fullscreen popup or audio evidence is claimed.
Original progress identity and revision remain `wwhd-alpha-1`; no migration or
copy of the user's progress was performed.

## Local checkout verification — 2026-10-28

The current main branch was cloned into a dedicated local development folder on
the original user's Windows PC. The official C# 5/x64 build succeeded, followed
by all 50 core/Win32 checks and all 22 UI checks. Existing local progress was
copied separately into the ignored `data/` directory; no personal state was added
to Git. The older alpha 0.1 workspace was preserved as a historical copy.
This run did not repeat live-game unlock or Cemu restart testing for alpha 0.3.

Date of initial live verification: **2026-10-28**, Europe/Rome.

## Alpha 0.3 system and catalog checks (2026-10-28)

Environment: Linux cloud container, Mono 6.8, C# 5 x64, libgdiplus and Xvfb.
No access to the user's Windows PC, Cemu or the game was used for this change.

- Application and both test executables compile at the C# 5 language level.
- **46 portable checks passed**, including the original 12 non-Win32 assertions
  and 34 new engine/schema/catalog checks. New cases cover stable low baselines,
  rejection of a low glitch on a completed save, observable confirmation state,
  invalid/null/timestampless snapshots, duplicate timestamps, gap resets,
  simultaneous atomic awards, simulated storage failure and recovery, immutable
  returned unlock records, game/revision progress isolation, a fixed legacy progress fixture, heart-capacity
  transitions, impossible rule
  thresholds and unknown attributes/elements, exact profile matching and blocked
  unavailable games.
- **22 UI smoke checks passed**: real mouse/keyboard dispatch opens the catalog,
  selects the current game, returns with B/Esc, exposes unavailable cards to
  accessibility clients, runs the existing demo through an unlock, pauses and
  resumes without losing the unlock, keeps recording independent of catalog
  navigation, fits the catalog at the supported minimum size, opens existing
  settings, discards demo progress on disconnect, blocks game switches while
  attached, switches a synthetic game after disconnect, preserves selection
  when its rules fail to load, clears displayed progress on profile edits and
  safely falls back from an unknown saved game ID.
- Visually inspected rendered main, demo, catalog, paused and settings views,
  including the minimum-size catalog. The palette, embedded source fonts,
  original sea artwork, medals, shadows and existing card layout are preserved.
  Mono uses installed Nunito, as explained in the alpha 0.2 record below.
- `achievements.xml`, all ten IDs, `wwhd-alpha-1`, memory bytes/address, original
  artwork/sound sources and legacy progress identity remain unchanged. The
  original rupee unlock evidence below belongs to alpha 0.1, not a live alpha 0.3 run.

The four native Win32 fixture assertions are explicitly skipped by `--portable`.
`tests/run.ps1` still runs the full suite on Windows; `tests/run-ui.ps1` compiles
and exercises the application in an isolated temporary folder. GitHub Actions
is configured to run both, build and verify a new alpha 0.3 package, and retain
the ZIP and UI previews. The actual Windows result is recorded below.

### Windows CI (2026-10-28)

Code commit `a66789f290b9c9702575da2c7e6d598ba526ec53` passed
[workflow run 40764665750](https://github.com/hoffo1987/WiiU-Achievements/actions/runs/40764665750)
on GitHub's Windows Server 2025 runner:

- Official .NET Framework x64 compiler, explicitly C# 5: application, core suite
  and isolated UI suite compiled successfully, with embedded Nunito resources.
- **50 core/integration checks passed**, including all four native Win32 fixture
  assertions for the synthetic child-process reservation, scanner and live reads.
- **22 UI checks passed** on Windows, without Cemu or any game files.
- `package.ps1` and `verify-package.ps1` succeeded; the manifest verified **47
  files**. The Windows-built ZIP and seven UI preview PNGs are downloadable as
  workflow artifacts.
- Downloaded and visually inspected Windows main, catalog, paused and
  minimum-size catalog previews. The original visual style is retained, with
  the new catalog and pause controls using the existing theme. Long achievement
  labels use the existing ellipsis behavior at narrower window widths.

These are build, synthetic integration and UI results. They do not verify a live
Cemu session, the user's display setup or fullscreen notification behavior.

Still pending: real Cemu thresholds and pause/load/resume, emulator restarts,
fullscreen popup, multi-monitor behavior, actual audio, DPI/screen-reader
behavior on the user's PC, and automatic menu/save-slot validity detection.
Manual pause provides a recording boundary; it does not discover game state.
The committed alpha 0.2 ZIP remains historical; rebuild current source or use a
successful alpha 0.3 CI artifact rather than expecting that ZIP to contain changes.

## Real game observations

Configuration: Cemu 2.6, Wind Waker HD EUR v0, Title ID `0005000010143600`,
RPX hash `cdc68acd`. English game, no enhancements enabled, initial controllable
Link sequence. The session's host base was discovered and matched Cemu's log;
its absolute address and PID are intentionally not prerequisites in this handoff.

- Read-only probe: 12/12 quarter hearts, zero rupees.
- User collected rupees and reported five; the probe read five.
- App connected and displayed 5 rupees, 3/3 hearts, zero local unlocks.
- User crossed the 10-rupee threshold and confirmed the notification appeared.
- App displayed **Primi spiccioli**, one point, 1/10 objectives; the probe later
  read 15 rupees. XML on disk contained `wallet-10` with a UTC timestamp.
- The app was closed and reopened while Cemu remained running. It recovered the
  same unlock and point and did not issue a new award on connection.
- The GUI was visually inspected and its accessibility text checked.

This is observed end-to-end evidence for one threshold achievement and saved
progress. It is not a completed-game certification.

## Automated checks

The original alpha 0.1 `tests/run.ps1` compiled a standalone test executable and
executed these 16 assertions (the current suite adds the checks listed above):

1. Big-endian rupee decoding and quarter-heart decoding.
2. Rejection of impossible health.
3. No awards for already satisfied initial state.
4. Three consecutive matches required.
5. Live threshold crossing persisted before reporting an award.
6. No duplicate awards.
7. Reloading persisted progress.
8. Separation of local profiles.
9. Transient spike rejection and match-count reset.
10. Sampling-gap reset.
11. Disconnect/reset baseline behavior.
12. Preservation of corrupt originals and previous-backup recovery.
13. Discovery of a synthetic child process's 4 GiB reservation.
14. Scanner matches across a 1 MiB chunk boundary.
15. Filtering out a stale duplicate after the child changes one value.
16. Reading the updated child value through real `ReadProcessMemory`.

These passed on the initial PC. A fresh package can run them without Cemu, a
ROM, Internet access, or changes to game memory. The child writes only to its own
synthetic allocation. The full GUI/session-log parser is not covered by this suite.

## Portable package checks

Verified on 2026-10-28 on the same Windows PC:

- The packaging command built a separate binary and passed all 16 assertions
  under Windows PowerShell without a .NET SDK or external package restore.
- The resulting ZIP was extracted to another folder with spaces in its name.
- Its manifest verified 23 listed files; the ZIP contained those files plus the
  manifest. Hidden project instructions were included.
- Source from the extracted copy rebuilt into another folder with spaces in its
  name, using an absolute script path from a different working directory.
- That rebuilt executable started in demo/preview mode, rendered the GUI, and
  exited successfully. It did not connect to or interrupt the user's live Cemu.
- Archive inspection found no local `data/`, settings, logs, ROM, or key entries.

This checks relocation on this PC, not a second physical PC or another OS.

`package.ps1` builds from source into a separate staging folder, runs the suite
unless `-SkipTests` is explicitly supplied, and writes a SHA-256 file manifest.
The package uses an input allowlist and excludes the original `data/` directory.
`verify-package.ps1` verifies the listed sizes and hashes after extraction.

The manifest is not signed and does not establish the author's identity. Rebuild
or edit a packaged file and its old hash will no longer match. Repackage after
intentional changes. Newly generated local `data/` is not a manifest entry.

## Alpha 0.2 interface checks (2026-10-28, Linux only)

Environment: an Ubuntu 24.04 cloud container with Mono 6.8 (`mcs -langversion:5
-platform:x64`), libgdiplus and Xvfb (1400×1000). Windows, Cemu and the game
were not available.

Checked:

- All sources compile at the C# 5 language level. The build uses the
  `build.ps1` references plus `Accessibility.dll`, and embeds the three Nunito
  resources. `tests/CoreTests.cs` still compiles against `Core.cs` and
  `MemoryReader.cs`. It was not executed, because its Win32 fixture needs
  Windows. `Core.cs`, `GameSession.cs` and `MemoryReader.cs` are byte-identical
  to alpha 0.1.
- Demo mode, driven with xdotool and screenshotted:
  - idle state and the staggered tile intro;
  - live progress bars;
  - the first unlock: "Verifica…" during the 3-sample confirmation, the tile
    celebration, the ring going from 0 to 10%, and the popup;
  - the 4-heart unlock at 16 quarters;
  - filters, the detail dialog and the log drawer;
  - the settings panel, with fields read-only during the demo and the toggles;
  - Tab, arrow, Enter, B and Y keyboard handling;
  - disconnect, which clears the demo progress from view.
- `--preview` and `--preview-toast` write PNGs.
- `Chime.Build()` returns a valid 1.3 s, mono, 16-bit, 44.1 kHz WAV with a 0.42
  peak and a smooth decay. This was checked numerically; nobody listened to it.

Mono-specific observations. They are not reproduced on .NET, but recheck them
on Windows:

- libgdiplus draws fonts added with `PrivateFontCollection.AddMemoryFont` as
  empty boxes. Under Mono the app therefore uses an installed "Nunito" family,
  which is how the screenshots were taken. On .NET the embedded fonts are used,
  but that path has not been run.
- During banner-only partial repaints, Mono sometimes shows stale back-buffer
  pixels in a 12 px band above the bottom bar. WinForms on Windows clips the
  paint DC to the update region. Confirm this visually.
- Only the non-layered fallback popup ran on Linux. The `UpdateLayeredWindow`
  path has never been executed.

Not verified for alpha 0.2:

- Anything on Windows:
  - the `build.ps1` csc build and the 16-check suite;
  - embedded-font and ClearType rendering, DPI scaling and text-box placement;
  - the layered popup over windowed and fullscreen Cemu, and its placement on
    multiple monitors;
  - `SoundPlayer` playback;
  - screen-reader output;
  - CPU use while playing.
- The included `WiiUAchievements.exe`, which was compiled with Mono `mcs` and has
  never been launched on Windows. Rebuild it with `build.ps1`.
- A live game session with the new UI. The connect, sample and disconnect code
  is the alpha 0.1 logic, but the UI around it is new.
- Packaging. The alpha 0.2 archive was assembled on Linux by a script that copies
  the `package.ps1` allowlist and manifest format. Neither `package.ps1` nor
  `verify-package.ps1` was run.

## Not yet verified

- The fixed guest header across multiple real Cemu restarts or a full game run.
- Increasing/decreasing health during actual gameplay or gaining heart capacity.
- Every rupee/heart achievement threshold in the real game.
- File-select screen, loading transitions, changing saves, and hero mode.
- Other game regions/updates, emulator versions, mods, or Windows environments.
- Inventory, story, bosses, collections, or any frame-specific challenge.
- Cross-folder concurrent writers, online sync, or anticheat.

## Manual regression before promoting a profile

Use a spare local achievement profile. Start with the app disconnected, open the
supported game and load a playable save. Confirm displayed hearts/rupees against
the game. Connect below a threshold, cross it, and verify a single notification
and saved entry. Reopen the app and confirm persistence without a second award.
Disconnect before loading a different save in this alpha.

For a future profile, also restart Cemu, recheck the discovered host base and
fixed/pointer-derived guest values, exercise menus and loads, and record the
results. If the user or game session is unavailable, report live validation as
pending rather than extrapolating from synthetic tests.
