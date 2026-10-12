# Architecture

## Active project

The repository root is a Unity 6.3 LTS project pinned in `ProjectSettings/ProjectVersion.txt`. On 2026-10-11 the user approved returning to Unity and bringing forward the released Godot demo's settlement features. `godot/` is retained as a compatibility reference and historical runnable release. Historical Unity baseline notes also remain under `UNITY_*`; current instructions are here and in the root README.

`Assets/AlphaHex/Scripts/Core` is pure C# 9 / .NET Standard 2.1. Runtime rendering, input, filesystem access and UI live in `Scripts/Runtime` and call validated `Simulation.Try*` commands. `tools/SimulationChecks` links production Core files into a .NET 10 verifier, not a separate implementation.

The fixed step is 0.125 seconds with a serialized remainder and integer step count. Runtime advances foreground time at 1× or 2×, capped at 0.25 real seconds per frame. There is no offline-production clock. Public fields and lists intentionally match Unity's `JsonUtility`; Core has no Unity or JSON-library dependency.

## People, resources and logistics

Homes and work are independent. Four pioneers live at the hall; cottages provide four additional places each. Supported immigration fills connected housing one person at a time in stable tile order. New citizens stay unassigned. Each workplace stores its explicit crew; pausing releases workers but retains legitimate work and reserved inputs.

Roads and buildings form a breadth-first route to the hall. Every producer owns at most one delivery and waits for it to arrive before another job. Deliveries cost 1.25 seconds per edge; migrated 0.75-second trips retain their original duration. The cart animation follows the saved route and never awards resources.

A kiln reserves clay and timber; a bloomery reserves ore and timber only after an explicit batch command; a foundry reserves ore and timber automatically. Input is removed from available stock once, retained through pause/save/load, consumed at dispatch, or refunded once on demolition. Stock, reservations and travelling goods are bounded together against counter overflow. Extraction depletes deposits at dispatch. Exhausted workplaces clear after their final delivery and leave an access road; managed logging remains for regrowth.

Research stores the current job and its paid progress. Completed flags have stable numeric values. Legacy milestone credits are retained only for the original farming, reforestation and deep-mining technologies. UI costs and recipes come from the same Core constants used by commands.

## Frontier and rendering

A new world reveals radius three (37 tiles) and owns seven. Seeded coordinate generation extends only when a connected watchtower surveys new land: radius four for wood, six for a reinforced tower. Revealed tiles and their discovery order persist after tower removal. The maximum radius is 16 (817 coordinates); the game is bounded, not endless.

Hex radius is 2.35 world units. Terrain meshes, selection rings, roads and picking use the same spacing and surface-height rules. Building and resource meshes are generated in code. Unknown space is drawn as a neutral mist border without colliders or hidden terrain data; camera limits track revealed land. Rendered citizens are cosmetic views of the census and crew allocation.

The code-built uGUI HUD uses `CanvasScaler.ScaleWithScreenSize`, a 1280×720 reference and width/height matching. Fullscreen therefore scales controls with the window instead of retaining tiny pixel dimensions. Buildings, workers and research use scrolling panels; fixed navigation stays outside scrolling content. The native player's opt-in QA captures actual layouts at several sizes. See [Unity's Canvas Scaler documentation](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html).

## Saves and migration

Unity schema 4 includes all resources, crew allocations, town/research clocks, discoveries, building jobs and shipments. A load validates structure, enum values, coordinates, contiguous claims, census, crew limits, prerequisites, reserves, jobs, routes, cargo timing and committed-goods totals before replacing the session. Required root fields are checked separately because `JsonUtility` can synthesize defaults for omitted objects.

Writes validate the state, write and flush a temporary sibling, read it back through the actual decoder, then replace the primary. Only a valid prior primary becomes the backup. If both startup saves are unreadable, automatic saving is disabled to preserve those files. Explicit New or Import archives the current village and writes the replacement before switching worlds. These protections are not a filesystem power-loss durability guarantee.

`Application.wantsToQuit` saves before a normal desktop quit and rejects the quit on failure. Save & Exit also returns success and closes only after a valid write. Updater installation uses this same path. See [Unity's quit event documentation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-wantsToQuit.html).

Unity schemas 1/2 pass a frozen legacy validator before migration of a copy. Schema 1 receives its original one-time pioneers, food and deep reserves. Job, immigration and sapling progress retain their completion fraction under the longer timings. Old automatic food-first staffing becomes initial manual assignments. Godot schema 3 uses explicit wire DTOs with numeric validation before narrowing counts and retains its discoveries, order, crews, timers, stock and cargo.

Existing mines/foundries receive the new prerequisite flags needed to remain usable. A foundry with two reserved ore completes that specific job without retroactively charging fuel; the next job uses the new recipe. A finite clay opportunity is added once on unused, non-mineral grass without replacing a building or exhausted deposit. If needed, a neighbouring unknown tile is introduced; a completely occupied legacy map is not overwritten. The original import file is never modified. No schema-4 state is repeatedly granted migration materials.

## Windows updates

`tools/WindowsLauncher` is a self-contained .NET 10 Windows launcher using pinned Velopack 1.2.161. `AlphaHex.exe` starts `AlphaHexGame.exe`. The game and helper share plain request/response DTOs in `UpdateProtocol.cs`. The helper reads only the fixed public repository and `win-playtest` channel; there is no player-side GitHub token.

Checking and downloading run in a separate helper process. Status files are replaced atomically and carry an operation ID. Velopack verifies package integrity, supports deltas and falls back to full packages. Downloads do not install on startup automatically. On an explicit install request, the game pauses and saves, the helper verifies the running game identity and pending version, then signals readiness. The player saves and exits; the helper waits for both game and original launcher to close before handing replacement/relaunch to Velopack. Timeout, missing feed or checksum failure is reported without resetting the settlement.

Save directories are outside versioned installation folders. The old Godot ZIP cannot update itself, so its users install the first Unity version once and import their settlement. Later release publishing is separate from ordinary source pushes. [Windows distribution](WINDOWS_BUILD.md) describes the release workflow and actual verification boundaries.

## Verification boundaries

`FrontierQa` only runs with `--alpha-qa`; all its files use the supplied `--qa-directory`. It verifies the actual player bootstrap, UI actions, source-retaining import, save continuity and failed Save & Exit. A rendered run adds screenshots, layout measurements and scaled-hex picking. A `-nographics` run explicitly reports that rendering was skipped. Neither replaces human usability, pacing or a physical Windows playthrough.

There is no server authority, multiplayer, combat, animal simulation, ship travel, arbitrary tunnelling or background service in this slice.
