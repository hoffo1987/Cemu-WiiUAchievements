# Cemu 2.6 with Wii U Achievements

Development fork based on official v2.6:
`a6fb0a48eb437a8a41c13b782ac8ae0433bf8f98`.

**Tools → Wii U Achievements** opens the achievement panel hosted inside Cemu.
The existing .NET Framework engine, artwork, local progress and optional cloud
client are reused in-process. The panel can be closed while recording continues.
The separate Wii U Achievements app must be closed to avoid concurrent archive
writers. No EmuHook or game-memory writes are used.

Supported game: The Wind Waker HD EUR v0, Title ID `0005000010143600`, RPX
`cdc68acd`. All 110 catalog objectives execute (93 experimental); the 40 formerly
planned targets share their implementation with the standalone app. This fork
does not certify the whole game set. Sources and limits are in
[the expansion ledger](wiiu-achievements-managed/docs/DETECTOR-EXPANSION.md).
Native snapshots follow submitted game frames, with menu/load/lifecycle guards.
Full Cemu/native and real gameplay validation status is in
[the guide](wiiu-achievements-managed/docs/CEMU-INTEGRATION.md) and
[validation record](wiiu-achievements-managed/docs/VALIDATION.md).

Run **Actions → Cemu with Wii U Achievements (Windows) → Run workflow** to build.
It first runs a native CLR/engine smoke fixture, then compiles Cemu and publishes
a development artifact. Keep its `WiiUAchievements/` directory beside `Cemu.exe`.
Use a separate directory from your original Cemu installation.

For local builds with VS 2022 C++ x64, Windows SDK, .NET Framework 4.8/4.8.1 SDK
and CMake, initialize upstream submodules and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File wiiu-achievements-managed/build-cemu.ps1
```

See upstream README/BUILD.md for normal emulator requirements. Cemu retains
its upstream MPL 2.0 licence. The managed project and artwork retain their
existing licensing status; this integration does not assign a new licence.
No games, keys, memory dumps, credentials or personal progress are included.
