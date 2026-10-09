# Verification record

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

Open the pinned Editor and follow [QA.md](QA.md). Record the commit and device environment with subsequent results. No device performance or App Store readiness claim is made by this starter.
