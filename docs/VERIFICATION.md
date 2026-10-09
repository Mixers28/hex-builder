# Verification record

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
