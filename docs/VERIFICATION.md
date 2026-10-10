# Verification record

## Public Windows prerelease — 2026-10-10

Published [v0.1.0-playtest.1](https://github.com/Mixers28/hex-builder/releases/tag/v0.1.0-playtest.1) in public `Mixers28/hex-builder`, targeting the tested game commit `f42965ab405ed74458911b5ebd6328ef2718544f`. Verified the release is published (`draft=false`) and marked as a prerelease. Its two uploaded assets are the Windows ZIP and an external SHA-256 checksum file.

The **37,984,055-byte** ZIP has SHA-256 `45cadaf0dbe3e3a4c7d5250edc11486c75c6babb18acee4eef43b574af823beb`. GitHub's asset digest matches the local tested artifact. Downloading through the public URL without authentication reproduced that hash and passed ZIP CRC validation. Release notes include installation, Save & Exit, source commit and verification limits. README and playtest handoff links now point to the hosted download.

This publication changes distribution and documentation only. The 86 simulation/save checks, 39 rendered checks and isolated lifecycle results below apply to the unchanged build; they were not rerun. Native Windows execution remains pending.

## Save & Exit and refreshed downloads — 2026-10-10

Added **Menu [M] → Save & Exit** to the current Godot game. The action saves synchronously and quits only on success. A storage failure displays the existing error and leaves the session open; normal window-close requests use the same checked write while autosaving is enabled. Existing unreadable-startup-save protection, schema and game rules are unchanged.

| Check | Result and scope |
| --- | --- |
| Import/compilation | Passed with Godot 4.7.2 on the Mac. Final import/export logs contain no script or engine errors. |
| Save/exit lifecycle | Passed in a copied project with a uniquely named `AlphaHexSaveExitQA-…` user directory. Created a village through normal commands, saved it, blocked the temporary-file path, then invoked the real menu signal and close notification. Both failures kept the session open and preserved the prior file. Removing the obstruction allowed the menu retry to write and quit with exit code 0. A second process restored the complete JSON-equivalent settlement, advanced it and exited through the close notification; the final on-disk state matched the expected record. No normal player saves were accessed. |
| Actual updated PCK: simulation/save checks | **86 passed, 0 failed** with the Mac engine. |
| Actual updated PCK: rendered checks | **39 passed, 0 failed**, including ten captures. The new action is visible and clickable at 1024×768; a real injected pointer click reaches its handler while QA protection prevents writing a normal save. Visually inspected the menu capture. |
| Windows ZIP | **6 files, 37,984,055 bytes**. Re-exported the EXE/PCK, refreshed the guide, and verified PE architecture, resource-pack header, CRCs and every packaged byte against the tested export. |
| Complete source ZIP | **28 files, 51,500 bytes**. Updated the existing source/configuration/UID whitelist and guide; CRCs and byte-for-byte source comparison passed. No separate clean-extraction suite was repeated for this update. |

The first isolated-project launch lacked the generated script-class cache; importing that test project resolved it. The reload fixture compares normalized JSON because Godot restores integer count fields while JSON parsing yields floating-point numbers; an independent JSON comparison also confirmed all saved values. Final lifecycle logs are error-free. The initial sandboxed editor run hit macOS certificate/settings permissions; the approved rerun passed.

Native Windows EXE execution remains **not tested**. The lifecycle check used headless Godot on Mac; window-close notifications were injected, not physical OS-button actions. The 124-check Retina fullscreen suite was not repeated because display settings did not change.

Downloads: [Windows ZIP](https://github.com/Mixers28/hex-builder/releases/download/v0.1.0-playtest.1/alpha-hex-first-frontier-windows-x64.zip), SHA-256 `45cadaf0dbe3e3a4c7d5250edc11486c75c6babb18acee4eef43b574af823beb`; [local Godot source ZIP](../Artifacts/alpha-hex-first-frontier-godot.zip), SHA-256 `49dfc2485d88224fcff25fec97030fa9d4afa1970d67945a36e6e9d253f88222`.

Evidence: [import](../Artifacts/save-exit/import.log), [lifecycle save/failure/retry](../Artifacts/save-exit/lifecycle-save.log), [lifecycle reload](../Artifacts/save-exit/lifecycle-reload.log), [packed rules](../Artifacts/save-exit/packed-rules.log), [packed runtime](../Artifacts/save-exit/packed-runtime.log), [menu screenshot](../Artifacts/save-exit/packed-runtime/10-save-exit-menu.png), [export](../Artifacts/save-exit/export.log), [package report](../Artifacts/save-exit/package.txt). The isolated fixture and expected records are retained locally under ignored `Artifacts/save-exit/isolated-project/`.

## Windows friends-playtest export — 2026-10-10

Produced a portable **Windows x86_64 release** with standard Godot **4.7.2.stable.official.ed1daf0bf** on the Apple M1 Pro Mac. The official export-template archive matched SHA-256 `f298490b8d44d934be425a5a65a51bf15f422428b229a06a6e11d9ffea248011` before extraction. The Compatibility renderer and Retina UI correction are retained; game rules and normal settlement persistence were not changed for packaging.

| Check | Result and scope |
| --- | --- |
| Release export | Passed, including import/script compilation. Final export logs contain no engine errors. Native EXE product/version metadata was applied. |
| Windows binary and package | Verified PE32+ Windows GUI executable, x86_64 architecture, Godot resource-pack header, ZIP CRCs and byte-for-byte correspondence with the exported files. |
| Actual exported pack: simulation/saves | **86 passed, 0 failed** using the Mac engine with `--main-pack` pointing at the delivered PCK. QA fixtures use an isolated output folder. |
| Actual exported pack: rendered game | **34 passed, 0 failed**, with nine frame captures, including workforce/research UI and compact-window input/layout. Visually inspected the packaged game's workforce frame. No script/runtime errors in the final logs. |
| Distribution | **6 files, 37,983,533 bytes** compressed: EXE, PCK, play guide, Godot license, third-party notices and checksums. Private settlements, screenshots, source documents and export templates are excluded. |
| Native Windows runtime | **Not run.** No Windows PC, VM or Wine runtime was available. Windows launch, graphics/performance, input and actual save-path behavior remain pending. Mac execution of the exported pack does not verify the Windows EXE. |

Initial pack checks exposed a QA-only assumption that `res://qa-output` was writable. The harness now accepts an absolute `ALPHA_HEX_QA_OUTPUT` folder and flushes injected input events before checking their effects. Both final packed-game suites pass. The rendered QA session starts with a fresh settlement and does not load or save the player's normal town. The prior 124-check fullscreen suite was not repeated for this packaging change; its separate result is below.

Original export SHA-256: `6bf6f142ee7dc6ffad844a9b6e892f68ffd59e6eb9abe43c5d8b4c473cbf0b15`; the Windows download was subsequently refreshed with Save & Exit as recorded above. The release is unsigned. Rebuild and first-PC playtest instructions are in [WINDOWS_BUILD.md](WINDOWS_BUILD.md).

Evidence: [export log](../Artifacts/windows-export/export.log), [package report](../Artifacts/windows-export/package.txt), [packed simulation/save log](../Artifacts/windows-export/packed-rules.log), [packed runtime log](../Artifacts/windows-export/packed-runtime.log), [workforce frame](../Artifacts/windows-export/packed-runtime/06-workforce.png), [research frame](../Artifacts/windows-export/packed-runtime/07-research.png), [compact frame](../Artifacts/windows-export/packed-runtime/09-compact-ui.png).

## Retina fullscreen UI correction — 2026-10-10

Reproduced the user's small-button report in the native Godot game on the Apple M1 Pro Mac: screen **3024×1964 at 2× Retina**, fullscreen game drawable **3024×1898**. The project had no content scaling, so its HUD retained the same pixel dimensions after the window grew. Earlier desktop checks did not cover fullscreen and missed this behavior.

Enabled `canvas_items` stretch with `expand` aspect using the existing 1440×900 reference. In the same fullscreen scene, the Build dock button grew from **119×49 pixels to approximately 250×103 pixels** (24.5 → 51.5 display points high). Text and other controls scale by approximately **2.1×**. The captured framebuffer remains **3024×1898**; the 3D world is not rendered at a reduced reference resolution.

| Check | Result and scope |
| --- | --- |
| Native fullscreen and resizing | **124 passed, 0 failed**. Waited for the macOS fullscreen transition to reach the actual display size; measured UI dimensions; captured fullscreen plus 1440×900, 1366×768, 1024×768 and 1920×1080 windows. Panel bounds and scaled pointer actions in Build, Workers, Research and the world passed. |
| Existing rendered game checks | **34 passed, 0 failed**, including the earned-resource frontier, construction, crew assignment, keyboard shortcuts, picking, synthetic gestures and compact layouts. |
| Native simulation/save suite | **86 passed, 0 failed**. No game rules or persistence code changed. The four additional local Unity fixtures were not rerun for this presentation correction. |
| Engine import | Passed; the new display-check script and UID are included. Final import, display, runtime and simulation logs contain no script or runtime errors. |
| Refreshed standalone ZIP | **28 files**; CRC, script UID presence and byte-for-byte comparison with the tested source passed. Cache, screenshots and private saves excluded. This update was tested from the workspace; the original clean-extraction run below applies to the earlier archive. |

The display test runs through `-- --qa --qa-display`; it uses a fresh, paused settlement and does not load or save the user's town. Pointer and key actions are injected through Godot's input handling with logical coordinates converted to window pixels. Frame captures were visually inspected. This is native rendered desktop evidence, not an unaided human usability test, physical touch-device test or export validation.

Evidence: [before fullscreen](../Artifacts/fullscreen-ui/display-before-02-fullscreen.png), [corrected fullscreen](../Artifacts/fullscreen-ui/display-02-fullscreen.png), [compact window](../Artifacts/fullscreen-ui/display-1024x768.png), [1920×1080](../Artifacts/fullscreen-ui/display-1920x1080.png), [before measurements](../Artifacts/fullscreen-ui/display-before-measurements.json), [after measurements](../Artifacts/fullscreen-ui/display-measurements.json), [display log](../Artifacts/fullscreen-ui/alpha-hex-display-final.log), [runtime log](../Artifacts/fullscreen-ui/alpha-hex-ui-runtime.log), [simulation log](../Artifacts/fullscreen-ui/alpha-hex-ui-simulation.log), [import log](../Artifacts/fullscreen-ui/alpha-hex-ui-import.log).

Delivery at this stage: [Godot ZIP before Save & Exit](../Artifacts/alpha-hex-first-frontier-before-save-exit-godot.zip). SHA-256: `f7deec74d84b69bdd50e1e6fddba4b83062b21bf6f38fd54311439ce4f0bd033`. Restart an already-running play session to load the display setting. Progression and save-schema behavior are unchanged.

## Godot First Frontier — 2026-10-10

Tested base revision `4150677` plus the Godot working-tree changes. Environment: macOS, Apple M1 Pro, standard **Godot 4.7.2.stable.official.ed1daf0bf**, OpenGL Compatibility renderer. The Unity project, Core source, asset GUIDs and original save artifacts were preserved.

| Check | Result and scope |
| --- | --- |
| Godot editor import and script registration | Passed. All 11 scripts registered; final engine logs contain no script/runtime errors. |
| Native simulation/save suite | **86 passed, 0 failed** without local-only fixtures. Covers gradual occupancy, manual crew limits/speed, partial work/reservations, delivery and finite-resource accounting, distance, recovery, regrowth/farming/deep mining, native validation/backup recovery, Unity schema fixtures, two earned-resource frontier paths and the 817-coordinate save boundary. |
| Existing Unity saves | **12 additional checks passed** across four existing local artifacts (98 total with the native suite). Imported, advanced and validated the town/depletion/HUD/village saves; SHA-256 comparisons confirm their source files were unchanged. |
| Rendered runtime | **34 assertions passed, 0 failed**, including 9 actual frame captures. Exercises pointer clicks in Build and Workers, keyboard construction/panels, Space after a HUD click, collider picking on terrain and a tall tower, synthetic pinch/release handling, compact-window button placement and the full resource-funded command scenario. |
| Desktop views | Inspected actual frames at **1440×900, 1366×768 and 1024×768**. Fixed off-screen anchored panels, triangle winding/picking, lighting, clipped controls, panel sizing, readable overlays and tall-building selection before the final pass. |
| Frontier feasibility | Seeds **417 and 418** both complete discovery → research/tools → staffed quarry → delivered stone → masonry → reinforced tower. Seed 417 takes **15.6 simulated minutes and 115 validated commands**, including manual gathering. Runtime displays restored checkpoints earned by this scenario; this is not an unaided human UI playthrough or a playtime estimate. |
| Repository checks | `python3 tools/validate_repo.py`, `git diff --check` and local documentation-link checks passed. Unity source/assets were not modified. |
| Complete project ZIP | Packaged **26 source/configuration/documentation/UID files**, excluding caches, QA output and private saves. CRC and byte-for-byte source comparisons passed. A clean extraction imported successfully and independently passed **86 rule/save checks and 34 rendered runtime assertions**. |

The first sandboxed clean-import attempt hit macOS certificate/editor-settings restrictions, and a sandboxed native-window launch was blocked. Repeating the checks with approved Godot access passed; the final clean-import, simulation and rendered logs contain no engine errors. These were environment restrictions, not ignored game failures.

Original delivery: [Godot project ZIP before the fullscreen correction](../Artifacts/alpha-hex-first-frontier-before-ui.zip). This archive matched the sources at that handoff; SHA-256: `a8532f40b35a1c893bd4a9c05f0d5091eb652ec145efc5107e45c33dcb7c454a`. The corrected delivery and current checks are recorded above.

Local evidence is in ignored `Artifacts/first-frontier/`:

- [Village](../Artifacts/first-frontier/02-village.png), [discovery](../Artifacts/first-frontier/03-discovery.png), [first stone delivery](../Artifacts/first-frontier/04-quarry-delivery.png), [reinforced tower](../Artifacts/first-frontier/05-expansion.png).
- [Workforce UI](../Artifacts/first-frontier/06-workforce.png), [research](../Artifacts/first-frontier/07-research.png), [1366×768](../Artifacts/first-frontier/08-desktop-1366.png), [1024×768](../Artifacts/first-frontier/09-compact-ui.png).
- [Simulation and legacy-import log](../Artifacts/first-frontier/alpha-hex-frontier-checks-final.log), [rendered log](../Artifacts/first-frontier/alpha-hex-runtime-final.log), [clean ZIP import](../Artifacts/first-frontier/alpha-hex-clean-import.log), [clean ZIP rules](../Artifacts/first-frontier/alpha-hex-clean-checks.log), [clean ZIP runtime](../Artifacts/first-frontier/alpha-hex-clean-runtime.log).

These local evidence files are not committed assets. Reproduce current screenshots with the README's rendered QA command.

Not established: human enjoyment, strategic differences across seeds, weeks-long progression, a complete human mouse-only playthrough, native file-picker/New/Previous interactions under every failure condition, full-map developed-town performance, physical touch devices, exported builds or iOS behavior. Synthetic gestures do not constitute device testing. Legacy .NET/Unity suites were not rerun because their code was unchanged; the active rules are covered by Godot checks.

Next playtest: support migrants, reassign scarce labour, reach stone and reinforce a tower, then observe whether another expansion feels worthwhile. Record repeated hand gathering, reserve shortages, waiting and the point at which the content feels exhausted. The remaining design assumption is explicitly open in [the slice record](slices/001-first-frontier.md).

## Town growth, research and original art — 2026-10-09

Tested base revision `59af330` plus the town working-tree changes on macOS, Apple Silicon, Unity `6000.3.26f1` and .NET SDK `10.0.401`.

| Check | Result |
| --- | --- |
| Standalone Core checks, Release | 35 passed. Covers supported/disconnected housing, immigration and meals, food recovery, staffing priority and pause, research costs and gates, replanted timber, deep-reserve conservation, save continuity, migration, invalid town state and town guidance. All prior production checks remain. |
| Unity EditMode tests | 13 passed, 0 failed or skipped. Real `JsonUtility` tests cover missing schema-1 fields, cargo-preserving migration, no repeated provisions, immigration, research and sapling continuity. Three asset checks verify imported town illustrations and their size/alpha settings. |
| Unity compile and Play Mode | Compiled and ran the existing island. The initial view retained 58 timber, 0 ore and 40 metal, added 24 food and four pioneers, and displayed the town controls. Observed citizen figures, worker counts and ongoing mine/logging/smelter deliveries during the user's live play. No runtime exceptions were found in the session log. |
| Save continuity | Backed up the original primary and fallback locally under ignored `Artifacts/town-check/`. The live game loaded schema 1 and subsequently wrote schema 2. The user continued developing that island during verification; their newer save was retained. |
| Original artwork | Generated house, farm and forester transparent PNGs with the built-in imagegen tool, copied into project Resources with `.meta` files, and connected to Town construction cards. Exact prompts are in `ART.md`. Imported at 256 pixels without mipmaps. |
| Repository checks | Required files, JSON, new asset metadata, unique GUIDs, Core boundary and whitespace checks passed. `AGENTS.md` was preserved. |

The three illustration size checks initially found that Unity ignored an incomplete default-platform import configuration; adding the default-platform maximum resolved all three failures. The generated PNGs themselves were preserved.

The editor was left running for the user's playtest. Automated input yielded when the user interacted with the window; full Town/Research panel interaction and a complete visual build-through remain manual QA items. Touch controls, physical-device safe areas, exported builds and iOS devices were not checked. Visible citizens are presentation figures, not independent family/pathfinding simulation.

## Compact HUD and expansion guidance — 2026-10-09

Tested base revision `27db9a6` plus the HUD/guidance working-tree changes on macOS, Apple Silicon, Unity `6000.3.26f1`, and .NET SDK `10.0.401`. The base revision checkpoints the previously verified extractor cleanup and generated Unity setup.

| Check | Result |
| --- | --- |
| Standalone Core checks, Release | 25 passed. New cases cover available/transit/reserved totals, waiting reasons and progress, a first production loop, saved depletion guidance, disconnected/starved producers, manual recovery, legal frontier steps, and finite resource exhaustion. |
| Unity EditMode tests | 8 passed, 0 failed or skipped. Includes real `JsonUtility` restoration of guidance after starter depletion. Results: ignored `Artifacts/editmode-results.xml`. |
| Repository and diff checks | Required files, JSON, asset metadata, unique GUIDs, Core boundary, and `git diff --check` passed. |
| Editor compilation and Play Mode | Compiled without script errors. Visually inspected the HUD at 1366×768 and 1024×768, including the open inspector and build controls. Verified I, G, M, Escape, tool selection, pause feedback, F5, fading notifications, and a repeated save notification. G selected and centred the suggested ore site without spending stock. |
| Existing settlement | Loaded the real save and confirmed “Restore ore supply” points to a fresh claimable deposit after starter mines/camps had cleared. Schema, seed, stock, tile data, and shipments matched the pre-HUD backup after UI checks; only the simulation clock advanced. |

The settlement backup is local under ignored `Artifacts/hud-check/before-hud-save.json`. Native automated pointer clicks in the Unity Game view did not reliably match the displayed pointer; mouse-button interactions are therefore inconclusive, not a pass. Keyboard controls exercised the same menu, panel and goal handlers. No new island or demolition was performed on the user's save. Active cargo accounting and reserved-input feedback were verified in Core tests; a full manual mouse/touch production loop, notched safe areas, exported builds, and iOS devices remain outstanding.

## Automatic extractor cleanup — 2026-10-09

Tested base revision `df81a13` plus the extractor-cleanup working-tree changes on macOS 26.6.2, Apple Silicon, Unity `6000.3.26f1`, and .NET SDK `10.0.401`.

| Check | Result |
| --- | --- |
| Standalone Core checks, Release | 19 passed. Includes final-delivery cleanup, manual depletion during work or delivery, disconnected sites, continued transit, legacy saves, resource accounting, and site reuse. |
| Unity EditMode tests | 7 passed, 0 failed or skipped. The two new cases round-trip final forestry and mine deliveries through `JsonUtility` before and after cleanup. Results: ignored `Artifacts/editmode-results.xml`. |
| Repository structure | Passed after the initial Unity import and generated asset creation. |
| Unity compilation and Play Mode | Passed. Loaded the existing settlement, observed two exhausted forestry camps and one depleted mine clear, and verified their access roads remained. The depot and smelter remained; the frontier mine continued extracting across the cleared site. A subsequent runtime save confirmed these states with schema 1 unchanged. No script compilation errors or runtime exceptions were found in the session log. |

The original settlement was backed up locally under ignored `Artifacts/depletion-check/before-change-save.json`. Exported builds, iOS device behavior, and the manual-removal confirmation timing scenario were not exercised in this session.

## Initial starter preparation

Prepared on **2026-10-09** for the initial repository commit.

### Executed

| Check | Environment | Result |
| --- | --- | --- |
| Standalone Core checks | Linux x86_64, .NET SDK 10.0.401, Release configuration | 15 passed, 0 failed. The runner links the actual `Assets/AlphaHex/Scripts/Core` files. |
| Repository structure | Python 3 | Required files, JSON configuration, all asset metadata, unique GUIDs, the starter scene's bootstrap reference, and Core's Unity-independent boundary passed. |
| C# syntax | SDK-provided Roslyn parser, C# 9 language mode | All 12 C# files under `Assets` parsed without syntax errors. This is not a Unity API type check. |
| Helper scripts | Python 3 parser and CLI help invocation | Both Python helpers parsed; the Unity helper's command interface ran successfully. |

The Core run covers generation, starting conditions, command validation and costs, manual depletion, connectivity, final deposit batches, automated resource conservation, shared refining reservations, safe demolition, production-chain accounting, active delivery and job save continuity, timestep consistency, and malformed state or tick rejection.

### Unity checks pending at preparation time

Unity Editor was unavailable in the preparation environment. Package resolution, Unity compilation, all five Unity EditMode tests, rendered scene review, Play Mode, exported builds, and physical-device checks remain pending. The console save tests use .NET JSON; the pending EditMode tests exercise Unity's actual `JsonUtility` path.

Open the pinned Editor and follow [Unity QA](UNITY_QA.md). Record the commit and device environment with subsequent results. No device performance or App Store readiness claim is made by this starter.
