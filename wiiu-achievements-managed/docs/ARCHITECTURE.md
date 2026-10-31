# Architecture and extension guide

## Runtime flow

The optional Cemu 2.6 development fork hosts this same managed assembly in-process.
`NativeCemuBridge` copies native submitted-frame packets into a bounded mailbox;
`MainForm.Native.cs` consumes fresh observations on the existing UI/session thread
and uses the same decoder, gameplay gate, engine, atomic archive and cloud client.
It does not open a process reader or parse Cemu logs. See `CEMU-INTEGRATION.md` for
lifecycle/reset semantics, native build requirements and outstanding live tests.
The flow below describes the independent application's unchanged external reader.

`Program.Main` enables WinForms and holds a named mutex derived from the app
user-data directory (`--preview-toast=<png>` renders one popup frame and exits before that). `MainForm` loads `achievements.xml`, then waits for a connection request
(or connects on `--connect`). `--demo` supplies synthetic snapshots with its own
progress directory. `--preview=<absolute PNG path>` closes the UI after rendering
an internal preview; it is a developer aid, not a game screenshot facility.

On connection, `MainForm.Connect`:

1. Finds exactly one Cemu process matching the selected executable path.
2. Reads title, version, RPX hash, memory base, and emulator version from the log.
3. Requires an exact match in the selected catalog entry’s profiles and creates
   that profile’s reader. Only Wind Waker is currently available.
4. Opens Cemu for query/read access and detects its 4 GiB guest reservation.
5. Requires the base from the log to match a discovered reservation.
6. Calls the profile reader. The current experimental reader reads a `0x724`-byte
   save-state window at `guestBase + 0x145B7B80`. It parses the original six-byte
   header, clothing bit and selected progression fields. The buffer is never
   written to disk. This is not an atomic or full-game snapshot.
7. Opens the selected profile's progress, establishes an engine baseline, then
   starts a WinForms timer with a nominal interval of 100 ms.

Each tick reads a snapshot, runs the engine, persists any awards, updates the UI,
and queues notifications. The interface is custom-drawn (`MainForm.Ui.cs`,
`MainForm.Toolbar.cs`, `MainForm.Cards.cs`, `MainForm.Detail.cs`, `MainForm.Library.cs`,
`MainForm.Tiles.cs`, `Theme.cs`, `Theme.Icons.cs`, `Motion.cs`, `Banner.cs`, `Notification.cs`). Its rendering model, animation timer,
input map and asset policy are described in `docs/UI.md`. UI state never feeds
back into the engine. The title fingerprint and memory base are rechecked
from the log approximately once per second. Failures disconnect the reader and
reset pending conditions. They do not erase saved unlocks.

## Types and responsibilities

`AchievementPresentation.Apply` loads the game's optional editorial metadata
file after rules, checks complete/unique ID coverage and the expected revision,
and applies rarity, order and optional text corrections. Other attributes are
rejected, so this layer cannot alter points or executable conditions. The shared
`AchievementRarity` value drives `Art.Tier` for cards/dialogs/toasts. A missing
editorial file fails before switching games; no live state is replaced.

`ProcessMemory` owns a native process handle and implements `Read`, `Regions`,
and `FindGuestBases`. Addresses are unsigned 64-bit host addresses. Guest offsets
are 32-bit and must be added to the discovered base. Native access is deliberately
limited to `OpenProcess`, `ReadProcessMemory`, `VirtualQueryEx`, and `CloseHandle`.

`Calibration` can scan readable committed MEM2 pages for an observed six-byte
header and filter candidates after values change. It checks alignment, overlaps
scanner chunks, supports cancellation, and caps excessive matches. It is a
research utility, not automatic profile validation and not a currently exposed
GUI workflow.

`GameCatalog` owns game definitions, set revisions, exact profiles and reader
factories. Its collections are immutable. `MainForm` keeps the selected game and
loads that game’s rules before changing selection. See `GAMES.md`.

`GameSnapshot` parses the six-byte big-endian header and provides named fields
to rules. `Observation.Validate` enforces 12–80 quarter-heart capacity, capacity
divisible by four, current health between zero and capacity, and 0–5000 rupees.
Those assumptions are part of the current profile; review them before extending
the game or supporting other titles.

The reader also decodes clothing mask `0x80` into nullable boolean-valued
`HeroClothes` (0/1). Header-only snapshots and the demo leave it unavailable,
which is distinct from an observed zero. Missing data clears that rule's transient
state and cannot arm acquisition. Other flags in the same byte are ignored.

`WindWakerProgression` contains 50 researched decoders for cumulative item flags,
individual songs, capacities, population counts, specific saved dungeon flags
and a few named event flags. Unknown encodings are unavailable rather than zero.
Values are stored in a bounded-name snapshot dictionary; setters reject unknown
fields and out-of-range values. Only supported definitions can reference them.
All rules using these new mappings must explicitly set `experimental="true"`.
They are executable development rules authorized for progressive playtesting,
not live-certified mappings. See `PLAYTHROUGH.md` and `MEMORY-MAP.md`.

`AchievementSet.Load` requires the revision supplied by the selected game
(the original overload still requires `wwhd-alpha-1`), unique IDs, nonblank
text, positive thresholds/points, and the fields `rupees`, `max_health` and
`hero_clothes` (only threshold 1), plus the registered experimental progression
fields with bounds/attainable capacity thresholds.
Unreachable thresholds, fractional heart capacities, unknown child elements
and unsupported condition attributes are rejected. Conditions
are currently a single `>=` threshold. Adding arbitrary XML fields alone does
not add an engine capability.

The additive catalog extension supports `<planned>` entries with an ID, category,
text, proposed points and a validation requirement, but no executable fields.
These entries are presented as `PendingValidation` and are skipped before any
condition or persistence evaluation. They cannot earn points, inflate completion
totals or unlock in demo mode. The main XML contains 16 executable rules; the
explicitly configured `achievements-story.xml` holds the tunic plus 53 executable
experiments; `achievements-planned.xml` contains 40 pending targets. All revisions must match
and IDs must be unique across files. The
companion may not contain executable rules. See `ACHIEVEMENTS.md`. Category/status selection is purely
presentation; rendering never alters sampling or persistence.

`AchievementEngine` evaluates snapshots without accessing the process or UI.
For each locked rule it maintains an armed flag and a consecutive-match count:

- Production explicitly uses `IncludeExistingSave`: a met condition in a real,
  ready gameplay context earns after three coherent confirmations, even if no
  below-threshold baseline was observed. The gameplay gate still excludes menus,
  loads and unstable contexts. Newly recognized conditions record observation
  time and use a distinct notification. Already stored IDs retain their dates.
- The two-argument engine constructor retains `NewEventsOnly` for legacy callers,
  demos and regression checks. In that mode a met baseline does not award/arm.
- Three consecutive below-threshold values arm the rule; a single low glitch
  cannot arm an already satisfied save. Below-threshold samples also clear a
  pending match count.
- Three consecutive at/above-threshold samples after arming award it.
- A timestamp reversal or sampling gap longer than one second resets all
  transient state and treats the next sample as a baseline.
- A regression in observed permanent progression also resets the baseline.
  Continuity stores a defensive copy so caller mutation cannot hide a rollback.
  Rupees, current health and consumable soup contents are excluded from this
  check. The independent gameplay gate handles observed scene/load boundaries; the regression check alone does not identify saves.
- Already persisted IDs never award again in that profile.
- Every award due in the same sample commits together via `AwardMany` before
  any award is returned to the UI. A write failure resets transient state and
  publishes no partial result.
- Null snapshots, missing/non-UTC timestamps and impossible values reset the
  engine and throw, including when callers bypass byte parsing.
- `Status(id)` returns a snapshot of the authoritative phase and confirmation
  count. `StateVersion` changes only when evaluation state changes, so the UI
  updates confirmations even when the numeric game values remain the same.
  The UI no longer maintains its own baseline mirror.

Manual pause stops the sampling timer, clears pending notifications and resets
the engine. Resume resets it again and rechecks session identity before sampling.
Earned progress remains. This optional user-controlled boundary is independent
of the automatic gameplay gate described below.

This is polling of persistent state, not event hooks or frame-perfect tracking.

Presentation sorts by optional `order` and combines chapter/category/status
filters. These metadata do not impose dependencies or feed the engine. The
sampling layer explicitly refreshes presentation when progression values change,
including below-threshold counters whose engine phase remains the same.

The toolbar in `MainForm.Toolbar.cs` additionally combines rarity, detection and
image availability ("Altri filtri") with tokenized accent-insensitive text search
including item aliases. It keeps an index for the selected rules list and offers
menus and presentation-only sorting by journey/rarity/points/name/earned date.
Changing a game resets the browsing choices.
No query, filter or sort value is passed to the engine or persistence identity.


## Recording boundaries and optional cloud (2026-10-30)

The redundant multi-field reset in the form has been removed. `GameplayContext`
reads live-checked stage/request/player signals before and after save bytes.
`GameplayGate` blocks title/file-select/loading/unknown snapshots and requires
750 ms of coherent context. Normal observed in-world scene requests can preserve
arming (not confirmation streaks); menu/unknown/gaps cancel continuity. A new
segment must pass the ready-context gate before production can recognize
already-satisfied conditions (`IncludeExistingSave`). Maximum-heart/permanent rollback
remains a separate defensive reset. `MainForm` displays automatic waiting without
stopping the sampling intent. A one-second watcher reopens only the same supported
executable/profile after loss; explicit disconnect invalidates pending attach
continuations. See [AUTOMATIC-LOADS.md](AUTOMATIC-LOADS.md).

`CloudSync` is used only by serialized `Task.Run` operations. A separate 30-second
UI timer and new local awards schedule synchronization without waiting inside
the sample loop. ProgressStore snapshots/merges stay on the UI thread. A returned
result is applied only if the same store is still selected and the form is alive;
demo never uploads or imports. Persisted imports reset transient engine state,
do not play award notifications, and preserve earned timestamps. Unknown/planned
IDs are rejected by the local merge. The local file is the durable upload queue;
failures retry the complete local set, not only new awards.

Firestore stores immutable per-ID UTC timestamp documents under
`users/{uid}/profiles/{SHA256(localProgressIdentity)}/unlocks/{id}`. Client create
preconditions prevent overwriting another device. Pagination, bounded responses,
JSON parsing, refresh/restart credentials and project-switch logout are handled.
Credentials are encrypted with per-user Windows DPAPI without `.bak` copies.
Backend Native REST compatibility, account provider and ownership rules must be
configured and verified separately. See [CLOUD.md](CLOUD.md).

## Persistence contract

Production paths are relative to `%LOCALAPPDATA%\WiiUAchievements`, shared by
release folders. `UserData` imports exact-identity legacy entries without
rewriting sources or existing timestamps; startup loads the saved progress view.
Explicit data directories (UI tests, previews, `--data-dir=`) skip legacy discovery.

Paths under that user-data directory:

- `settings.xml`: local Cemu path, profile name, selected catalog `game` ID, and the optional `sound` and
  `ambient` UI toggles. Missing toggles default to enabled.
- `progress/<key>.xml`: earned IDs with UTC timestamps.
- `demo/<random ID>/`: isolated synthetic-session progress.

The identity string is exactly:

```text
trimmedProfileName|titleId|titleVersion|rpxHash|setRevision
```

The filename key is the first 24 hexadecimal characters of SHA-256 of that UTF-8
string. XML timestamps use the round-trip UTC format. `SafeXml` prohibits DTDs
and external resolution. Writes use a same-directory temporary file followed
by `File.Replace` (or `File.Move` on the first save). A `.bak` contains the previous
save, so recovery can lose the most recent award if the newest file is damaged.
Damaged originals are preserved before recovery.

Stable IDs are the persistence interface. Cosmetic renames need not change IDs.
Changing IDs or the revision requires an explicit compatibility/migration plan.
The mutex is keyed by the canonical user-data directory, so different release
folders using the same archive cannot run concurrently. Different explicit test
roots remain independent. Old binaries still use their legacy private archive.

## Extending without losing the baseline

For a new observed value, document its mapping and validation status first.
Add parsing and meaningful bounds to `GameSnapshot`, expose it in `Value`, and
extend the rule schema/loader only as required. Add regression tests covering
initial loaded state, a real transition, temporary spikes, and disconnect/load
boundaries. Retain existing progress IDs.

For game-state/save-slot detection, introduce explicit validity/identity in the
snapshot/monitoring boundary. Reset the engine when a transition invalidates
continuity. Avoid embedding all future event logic in WinForms timer handlers.

The build embeds `assets/fonts/*.ttf` as resources and references
`Accessibility.dll` for the custom accessible objects.

Profile metadata and the memory adapter now live in `GameCatalog.cs`. The
snapshot fields and threshold schema are still Wind Waker specific: a future
game must add its required data validation and rules with its adapter.
`tests/UiTests.cs` covers catalog navigation, unavailable cards, recording
pause/resume, settings and minimum-size layout. The session-log parser still
lacks dedicated automated coverage; engine/UI tests do not prove live Cemu behavior.

## Current presentation assets

`BrandArt.cs` owns shared project branding; `GameStyle.Assets.cs` fits game
images proportionally and builds cached simple borders from uniform corners
and repeated edges. Status values occupy a separate banner row. These layers
do not feed engine or persistence state. `tools/render-docs.ps1` regenerates all
public screenshots with isolated synthetic data; see `UI.md`.
