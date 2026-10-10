# Architecture

## Active project and boundary

The active game is `godot/project.godot`, using standard Godot **4.7.2** and GDScript. `godot/engine-version.txt` records the editor patch. The root Unity project and C# console verifier are preserved as a [legacy baseline](UNITY_ARCHITECTURE.md); they are not dependencies of the Godot game.

The user requested the engine change on 2026-10-10. This deliberately moves active persistence from Unity public-field `JsonUtility` records to validated Godot JSON dictionaries/arrays. No Unity Core files, `.meta` GUIDs or original saves were rewritten.

| File under `godot/` | Responsibility |
| --- | --- |
| `core/rules.gd` | Costs, times, research prerequisites, capacities and hex arithmetic. |
| `core/simulation.gd` | Authoritative state, deterministic region generation, validated commands, fixed-step town/production rules, routes and save validation. |
| `core/save_store.gd` | Versioned JSON I/O, temporary-file replacement, backup recovery, Unity schema 1/2 conversion. |
| `runtime/game.gd` | Session lifecycle, camera/input, command feedback and save/import actions. |
| `runtime/world_art.gd` | Original procedural meshes, palette and staged excavation surfaces. |
| `runtime/world_view.gd` | Revealed terrain/colliders, fog border, selection, census figures and actual shipment carts. |
| `runtime/game_hud.gd` | Resource strip, construction, workforce, research, inspector and menus. |
| `tests/simulation_checks.gd` | Headless checks against actual Core scripts, including serialization and storage. |
| `tests/frontier_scenario.gd` | Reproducible resource-funded play path through normal simulation commands. |
| `tests/runtime_smoke.gd` | Rendered input/UI checks and screenshots; no normal-save access. |
| `tests/display_checks.gd` | Native fullscreen and resize regression checks, measured button dimensions, framebuffer captures and scaled pointer input. |

## State and commands

`Simulation.state` owns town-wide data; `Simulation.tiles` maps canonical axial coordinate strings to revealed tile records. Meshes, controls and moving people are views, not alternate inventories. Runtime submits `try_claim`, `try_build`, `try_road`, `try_workers`, `try_pause_work`, `try_gather`, `try_forage`, `try_research`, `try_upgrade` and `try_remove`. Invalid commands explain the rejection without partial spending.

The simulation advances in fixed 0.125-second steps with a serialized remainder. Runtime supplies foreground time at 1× or 2×, caps a single frame's contribution at 0.25 seconds, and supports pause. The clock does not consult real time on load. New seeds use real time only when explicitly creating another settlement.

Homes and employment are independent. Four pioneers live at the hall; each cottage adds four places. Supported migrants occupy connected housing in stable discovery order. Production crews are explicit saved counts, bounded by each workplace and the total population. A new migrant stays available until assigned. Crew changes retain legitimate work and foundry reservations; pause releases a crew.

## Regions and presentation

New games generate only a radius-three starting area. Seed and integer coordinate hashing determine subsequent terrain/deposits; guaranteed starting resources and the first stone opportunities keep the initial research chain reachable. A connected wooden tower reveals radius four; reinforcement reveals radius six. All generated coordinates are within radius 16 (817 total). Revelation creates no ownership or stock, never replaces existing deposits, and survives tower removal.

Unknown cells have no simulation tile and cannot be inspected or claimed. Presentation generates only a neutral two-ring fog border beyond known terrain and an unbounded-looking background; it does not render hidden terrain or expose the maximum boundary through camera limits. Camera bounds follow revealed tiles. The world is bounded, not an endless-world implementation.

Hex radius is 2.35 world units with small cottages, yards and work details within each plot. Extraction changes surface height in three surface-ore stages, three deeper stages and three stone stages. Roads and picking colliders use the same surface function. ArrayMesh triangle winding is clockwise, with outward normals.

Citizen figures follow cosmetic home/work routes derived from census and crews. Shipment carts interpolate the exact saved route and elapsed/duration values. Neither animation awards goods. Terrain refresh signatures include changed structures, resources, excavation and sapling stages. Rendering a full supported map has a different evidence requirement from validating its save size.

The HUD uses a 1440×900 reference with `canvas_items` stretch and `expand` aspect, following Godot's [multiple-resolution guidance](https://github.com/godotengine/godot-docs/blob/4.7/tutorials/rendering/multiple_resolutions.rst). Logical Control coordinates scale with window size, including macOS Retina fullscreen, while the framebuffer remains at native window resolution. Existing anchors and resize layout handle the extra space at different aspect ratios. OS input is transformed by Godot; runtime must not apply a second UI scale to mouse coordinates. Synthetic input tests convert logical positions to window pixels with `Viewport.get_final_transform()` before calling `Input.parse_input_event()`.

## Goods and logistics

Roads and buildings form a breadth-first route to the single village hall. A producer dispatches one batch and waits for that cart to arrive before working again. Travel costs 1.25 seconds per edge; imported cargo retains its original 0.75-second-per-edge trip until delivered. Longer routes still reduce throughput even with more workers.

A foundry reserves two ore from shared stock before work and turns them into one metal. This is not an inbound ore delivery simulation. Stored, reserved and travelling goods are accounted for separately and bounded together by the resource counter capacity. Manual resource recovery credits stores directly on owned land.

Finite deposits decrease at dispatch, never at both dispatch and arrival. Depleted extractors clear after final delivery, releasing crews while retaining access roads. Logging beside a tree nursery remains for regrowth. Removal refunds no construction materials, returns unused reserved ore once, and rejects occupied houses, the hall and active cargo routes. UI removal confirmation is tied to the current building/road so automatic depletion cannot reuse a building confirmation to remove its road.

## Persistence

Schema 3 stores counts, flags, resource reserves, worker/resident allocation, clocks, research jobs, discoveries and shipments as plain JSON values. JSON numbers are normalized to integer count fields only after validation. Tile discovery order is retained because it breaks ties for immigration and work.

Load validates coordinates, uniqueness, terrain/deposit compatibility, contiguous claimed territory, census and crew limits, prerequisite-backed buildings/research, job/input consistency, cargo sources/routes/times/batches, and total committed goods before changing the session. Rejected input leaves live state unchanged.

Writes validate first, flush a temporary sibling file, preserve the previous valid primary as `.bak`, then rename the temporary file. A corrupt primary never replaces a good backup. These protections cover normal application failures; they are not a claim of fsync-level power-loss durability. Startup tries primary then backup; failure disables autosave until an explicit save or replacement.

`game.save_game()` reports success. The menu's Save & Exit action calls it synchronously and quits only after a successful write; failure leaves the session open with the storage error. Window-close requests use this same path when autosaving is enabled. If startup disabled automatic writes, window closure retains its existing no-write behavior to protect the unreadable save. QA sessions cannot save or exit through the menu action.

The Unity importer copies schema 1/2 records. It retains all 127 known coordinates, finite reserves, stock, houses, residents, research, meal/migration/forage clocks, paused work, foundry input and travelling cargo. Job progress retains its completion fraction under the new durations. Sapling growth is scaled from 45 to 90 seconds. Schema 1 receives its one-time four-pioneer/24-food/deep-reserve migration. Schema 2 retains its town. The old food-first, coordinate-ordered staffing becomes initial manual crew assignments; further arrivals do not auto-assign. Unspent old milestone credits remain valid for the original three technologies. The original selected file is never written.

Only successful archive and new-save writes replace a settlement during New or Import. There is one separate previous-settlement slot, not a history or a swap feature. Native and legacy user directories stay separate.

## Verification boundaries

`godot/export_presets.cfg` defines a Windows Desktop x86_64 release, using pinned official templates cached outside the active project. `tools/export_windows.py` verifies templates, exports the EXE/PCK, includes the play guide and engine notices, and packages a fixed file list. See [Windows builds](WINDOWS_BUILD.md). The release's resource pack is verified with the same QA scripts as the workspace; `ALPHA_HEX_QA_OUTPUT` selects an absolute writable test directory because packed resources are read-only. Gameplay continues to use its existing `user://` save store.

Headless checks preload production scripts rather than reimplementing the rules. The scripted frontier scenario spends through validated commands and uses the documented manual recovery actions; focused edge-case fixtures may deliberately set prerequisite or resource state. Rendered QA separately tests controls, picking, layouts and captures actual game frames.

[VERIFICATION.md](VERIFICATION.md) records what ran. Automated inputs and synthetic gestures do not establish human usability, enjoyable pacing, weeks of replayability, mobile performance or export success. The game has no network authority, multiplayer, combat, arbitrary tunnels or background simulation service.
