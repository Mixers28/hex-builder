# Unity QA and acceptance checks

These are checks to perform, not claims that every item passed. Actual results are in [VERIFICATION.md](VERIFICATION.md). The [old Godot checklist](GODOT_QA.md) remains available for its preserved demo.

## Automated checks

```sh
dotnet run --project tools/SimulationChecks/SimulationChecks.csproj --configuration Release
python3 tools/validate_repo.py
python3 tools/unity_cli.py test
dotnet run --project tools/WindowsLauncher/WindowsLauncher.csproj --configuration Release -- --self-test
git diff --check
```

The Core verifier links production sources. It covers the zero-metal route, manual crews, conservation of finite deposits and reserved inputs, carts, migration, research, discovery, invalid records and old Unity/Godot save migration. Unity EditMode tests use actual `JsonUtility`, including real disk writes, backup recovery and rejected writes. Launcher tests use real Velopack feed/download/checksum code with disposable local packages.

## Exported players

Build with `python3 tools/unity_cli.py build-macos` or `build-windows`. Run the actual exported executable with `--alpha-qa --qa-directory /absolute/isolated/folder --qa-fixtures /absolute/repository/Assets/AlphaHex/Tests/Fixtures -logFile /absolute/log/path`. Never use a real player's save folder as the QA directory. Add `-batchmode -nographics` only for headless checks.

Rendered QA captures the village, clay/kiln, manual bloomery, exploration, research, workers and menu at several resolutions and fullscreen. It measures visible navigation bounds/button heights and checks scaled hex picking. Its presentation fixture provisions resources deliberately; use the command-only verifier for economy feasibility. Both rendered and headless checks exercise save/reload, source-preserving Godot import and successful/failed Save & Exit. JSON evidence explicitly records whether rendering was skipped.

The [Windows release gate](WINDOWS_BUILD.md) tests the native executable, installer and actual update/restart in disposable paths. `ALPHA_HEX_QA_UPDATE_DIRECTORY` and `ALPHA_HEX_QA_UPDATE_VERSION` are opt-in integration-test variables inherited through that restart. They must never be set for an ordinary play session.

## Human desktop playtest

- Begin with zero metal. Keep food and timber available, build connected empty cottages and observe gradual arrivals. Assign/reassign workers and check crew limits, pending inputs and cargo before and after saving.
- Gather clay, fire bricks, research primitive smelting, run manual bloomery batches, then earn research and construction costs for a foundry. Continue to a useful watchtower discovery. Record elapsed human time, idle waiting, manual-click burden and unclear costs.
- Try two seeds. Observe whether terrain, route length and worker allocation create different decisions; cosmetic variation alone is not replayability.
- Pan, orbit, zoom, pick terrain/buildings, drag across panel boundaries and use keyboard shortcuts after clicking controls. Typing an import path must not trigger game actions.
- Inspect construction, workforce, research, inspector and menu at laptop size and fullscreen. Confirm text/button readability and scrolling. Automated bounds checks do not establish human readability.
- Save & Exit, reopen, load Previous settlement, and import a copy of an old Godot save. Confirm the source remains unchanged and the old Unity village was archived. Test corrupt-primary recovery and failed writes only in disposable QA directories.
- Download an update while playing, then Save, update & restart. Confirm the same village returns. Report the exact Windows warning text and displayed publisher, if any.
- Play a developed settlement long enough to measure frame time and memory. Full-map save validation is not a performance test.

The current human target is 45–60 minutes to assess the deeper opening. Automated simulated completion time is not a measured human session. This slice does not establish weeks-long content, combat, naval expansion, animals or offline progress. No mobile, touch or iOS pass is implied; physical-device checks are separate.
