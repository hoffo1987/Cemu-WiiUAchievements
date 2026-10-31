# Memory profile — EUR v0 only

## Address model

Cemu 2.6 reserves a contiguous 4 GiB host virtual-address range for the Wii U
address space. `ProcessMemory.FindGuestBases` groups regions by `AllocationBase`,
requires a 4 GiB reservation and readable code/MEM2 pages. `MainForm` cross-checks
the candidate with the base written into the active Cemu log.

```text
host address = current guestBase + Wii U guest address
```

Do not persist or reuse a previously observed host base or process ID. The guest
header address below is still a fixed profile assumption, not a discovered symbol.

## Automatic context signals (2026-10-30)

The production reader additionally reads 26 bytes at guest `0x104741E4` and 16
bytes at `0x10474BDC`, on both sides of the progression-window read. Current/next
stage, request byte at +0x18 and two Link references are checked. `Name` and
`sea_T` cannot count as playable worlds. Exact EUR file-select/load/restart,
inventory and area evidence, source derivation, stability rules and remaining
cases are recorded in [AUTOMATIC-LOADS.md](AUTOMATIC-LOADS.md). These are independent
of the experimental progression decoders below. No host addresses are persisted.

## Current fields

The **Playthrough TEST** development build now reads the `0x724`-byte window
starting at the header below and decodes only the selected fields listed here.
It never saves this buffer. The user explicitly requested executable batches
for later in-play verification: the new mappings remain **experimental**, even
when a baseline looks plausible. They are not promoted to live-verified support.

| Relative guest offset | Decode / intended meaning | Evidence |
| --- | --- | --- |
| `+0x51..+0x65` | Get-item flags: telescope, sail/Swift Sail, Wind Waker, bags, grappling hook, leaf, boomerang, camera/upgraded camera, boots, armor, bow/elemental/light arrows, bombs, hookshot, hammer | Public HD trainer mapping; individual set bits; runtime masks reject unsupported patterns |
| `+0xB4..+0xB6` | Cumulative sword, shield and bracelet bits | Public HD mapping plus acquisition semantics from decompilation |
| `+0x12`, `+0x13`, `+0x6F`, `+0x70` | Wallet level → capacity; magic/arrow/bomb capacities | Public HD mapping; constrained upgrade levels |
| `+0xBD`, `+0xBE`, `+0xBF` | Six song bits, eight Triforce bits, three pearl bits | Public HD mapping; songs/Triforce use popcount; pearl order Nayru/Din/Farore is 0/1/2 |
| `+0x4A..+0x4D` | Four bottle item slots: `FF` absent, `50..59` known contents | Inventory index derivation; invalid slots suppress bottle count |
| `+0x104..+0x134` | 49 fishman chart-sector bytes, count bit 0 in allowed values `0..3` | Public HD mapping |
| `+0x380 + stage*0x24 + 0x21` | Saved dungeon item byte, boss bit `08`; stages FF=2, DRC=3, FW=4, TOTG=5, ET=6, WT=7 | GameCube structure derivation; **HD mapping is unverified**; saved flags may update on exit/warp |
| `+0x633` mask `80` | MET_KORL after first fortress | Named event `0F80`, event-array layout hypothesis |
| `+0x650` mask `02`, `+0x651` mask `02` | Broken barrier, Zelda awakened | Named events `2C02`, `2D02`, layout hypothesis |
| `+0x64E` mask `20` plus soup in bottle | Grandma healed together with soup obtained | Named event `2A20`; compound condition, not a generic bottle award |

Sources: [HD offsets](https://github.com/jamesst20/ZeldaWindWakerTrainer-for-CEMU/blob/master/ZeldaWindWakerTrainer/GameOffsets.cs),
[save structures](https://github.com/zeldaret/tww/blob/main/include/d/d_save.h),
[item acquisition](https://github.com/zeldaret/tww/blob/main/src/d/d_item.cpp),
[named events](https://github.com/zeldaret/tww/blob/main/include/d/d_save_event_flag.inc),
[inventory/symbol indices](https://github.com/zeldaret/tww/blob/main/include/d/d_com_inf_game.h).
No source implementation was incorporated. `WindWakerProgression.cs` is the
independently implemented C# mapping; `ProgressionTests.cs` reconstructs fixtures.

**Passive baseline (2026-10-28):** exact Cemu 2.6 / EUR v0 / `cdc68acd`, reservation
and log matched, no user actions requested. All 50 decoder fields were available:
initial-item/story/song/boss/count flags zero, wallet capacity 500, three mapped
sectors, zero bottles, while the separately verified clothing bit was set.
This is only one compatible early-game baseline. Future acquisitions, late-game
stage semantics, menu/load/reconnect/restart behavior of each new mapping remain
pending. Only the earlier clothing/rupee evidence below is live-confirmed.

- Header guest address: `0x145B7B80`.
- `+0x00`, two bytes, unsigned big-endian: maximum health in quarter hearts.
- `+0x02`, two bytes, unsigned big-endian: current health in quarter hearts.
- `+0x04`, two bytes, unsigned big-endian: current wallet rupees.

Example **synthetic** byte sequences, matching observed values:

```text
00 0C 00 0C 00 00  -> 3/3 hearts, 0 rupees
00 0C 00 0C 00 05  -> 3/3 hearts, 5 rupees
00 0C 00 0C 00 0F  -> 3/3 hearts, 15 rupees
```

These examples are reconstructed from the parsed values, not uploaded game dumps.

## Evidence

The initial address reference came from the public
[GameOffsets.cs mapping](https://github.com/jamesst20/ZeldaWindWakerTrainer-for-CEMU/blob/master/ZeldaWindWakerTrainer/GameOffsets.cs).
The application and memory reader were implemented independently. The observed
session on 2026-10-28 confirmed 0 → 5 → 15 rupees and 12/12 quarter hearts. The
user confirmed the 5-rupee display and the 10-rupee achievement notification.

The address `0x1506B500`, mentioned in other research, was also probed and did
not produce a valid header in that session. It is **not a supported fallback**.
It appears in the optional probe only to distinguish the earlier research cases.

The address model is based on
[Cemu 2.6 MMU.cpp](https://github.com/cemu-project/Cemu/blob/v2.6/src/Cafe/HW/MMU/MMU.cpp)
and
[MemMapperWin.cpp](https://github.com/cemu-project/Cemu/blob/v2.6/src/util/MemMapper/MemMapperWin.cpp).

## Unverified assumptions and research protocol

### Initial inventory baseline — 2026-10-28

Manual targeted read on the original user's Windows PC: Cemu 2.6, EUR v0,
RPX `cdc68acd`; log identity and 4 GiB reservation matched. User reported having
received none of the telescope, sword or shield. The known header read 12/12
quarters and 17 rupees. Three candidate single-byte reads returned zero:

- `0x145B7BD1` (header + `0x51`): telescope hypothesis.
- `0x145B7C34` (header + `0xB4`): sword hypothesis.
- `0x145B7C35` (header + `0xB5`): shield hypothesis.

Candidates originate from the linked trainer's public mapping, not copied code.
**Status: baseline observed once; acquisition semantics unverified.** No before/
after acquisition, menu, area, save/load, reconnect or restart check has passed
for these fields. Zero matching absence does not identify a usable acquisition
flag. These offsets appear only in the manual `tests/InitialItemProbe.cs`, never
in the production reader. No raw dump, personal log, host address or PID is saved
in the project. The 94 new catalog targets remain disabled.

The user later clarified that the green tunic had already been received from
Grandma. The zero-byte baseline above concerns telescope/sword/shield only. At
that point no clothing address or event flag was known; `story-heros-clothes` is
the first pending story target, not a retrospective unlock.

### Clothing flag research — 2026-10-28

After the user repeated the tunic acquisition in a fresh Cemu session matching
the exact supported profile, a targeted read of `0x145B81CE` returned `128`
(mask `0x80` set). Known header: 12/12 quarters, 15 rupees; telescope, sword and
shield candidates remained zero. **Observed once after acquisition only**.

The hypothesis is derived from the public GameCube decompilation, not an HD
memory map: [player initialization](https://github.com/zeldaret/tww/blob/main/src/d/actor/d_a_player_main.cpp)
checks event `0x2A80` to choose the clothing appearance (also accounting for clear
count). [Save layout](https://github.com/zeldaret/tww/blob/main/include/d/d_save.h)
places the event array at save `+0x624`, and [event-bit indexing](https://github.com/zeldaret/tww/blob/main/src/d/d_save.cpp)
uses the high byte as the array index and the low byte as a mask. Thus the
candidate is header `+0x624+0x2A = +0x64E`, mask `0x80`. HD layout compatibility
is still a hypothesis; the before/after, menus, loads and reconnect checks are
pending. No decompiled implementation is incorporated and no raw game dump is
saved.

Further live checks in the same exact profile:

1. After the user loaded the earlier save and stopped before Grandma's delivery,
   the byte was `0`, mask clear. Header remained 12/12 quarters, 15 rupees.
2. After receiving the tunic and regaining control, the byte was `128`, mask set.
3. After leaving Grandma's house and opening the game's pause/inventory menu,
   the byte remained `128`, mask set. Other initial-item candidates remained zero.
4. Each observation reopened the read-only handle, reidentified the exact profile
   and matched the guest reservation; it did not reuse a host base or PID.

**Status: observed acquisition/load/menu/area cycle, enabled experimentally for
EUR v0 only.** The production reader now reads this one extra byte and exposes
only mask `0x80` as boolean `hero_clothes`. `story-heros-clothes` is active and
keeps its existing ID/revision. No other inventory candidates are promoted.
The earlier event-only policy is superseded for production: a stable loaded
flag can now be recognized after the gameplay gate and confirmations. The demo
does not simulate this flag. Automatic load/reconnect flows are documented in
`AUTOMATIC-LOADS.md`; save slots still share the local profile identity. A full-game
run remains unverified. This is a targeted map validation, not a certified profile.
The user subsequently confirmed the corrected application's tunic unlock popup
during a new acquisition; the local progress XML contained exactly one saved
`story-heros-clothes` entry. The original source ID/revision were retained.

The fixed header's stability across real Cemu restarts, game loads, all menus,
and the full game remains limited to the recorded live evidence. Gameplay
scene signals are documented above; other inventory/boss candidates retain
their individual experimental status. Do not infer those offsets from the header's
successful rupee test.

For each future field record:

1. Exact emulator/title version, region, Title ID and RPX hash.
2. Guest address or pointer derivation, width, byte order, and bounds.
3. Before/after values and the user-observed action they correspond to.
4. Whether the value is current, a stale copy, UI-only, or saved state.
5. Menu, area transition, save/load, and reconnect behavior.
6. Separate status: hypothesis, observed once, repeatable, or supported.

Use small targeted read-only observations. Keep raw memory and personal log
files out of the shareable project. Record facts and short sanitized examples.
