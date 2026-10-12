> Historical Godot checklist. See [QA.md](QA.md) for the active Unity game.

# QA and acceptance checks

This is the active Godot checklist, not a claim that every item passed. Recorded results are in [VERIFICATION.md](VERIFICATION.md). The previous [Unity checklist](UNITY_QA.md) remains available for legacy changes.

## Required local checks

Use standard Godot 4.7.2; open `godot/project.godot` and allow the initial import. From the repository root:

```sh
godot --path godot --headless --editor --import --quit
godot --path godot --headless --script res://tests/simulation_checks.gd
godot --path godot -- --qa
python3 tools/validate_repo.py
git diff --check
```

Inspect logs for script errors as well as checking exit status. Rendered `--qa` uses a fresh isolated session, does not touch normal user saves, and writes actual frames to ignored `godot/qa-output/`. Core checks use temporary save paths in that directory and remove their own temporary JSON files. Optional `--legacy=<path>` arguments after `--` read older save copies and verify their hashes remain unchanged.

The active automated checks cover migration/occupancy, crew caps and speed, fixed-step save continuity, reserved inputs, finite surface/deep ore, renewable timber, farm output, cargo delay/accounting and safe removal, distance-sensitive throughput, malformed saves, native backups, Unity schema migration, two complete resource-funded frontier scenarios, permanent discovery and the 817-coordinate save boundary. Focused fixtures set up edge cases directly; the frontier scenario earns materials and research using validated commands.

## Desktop interaction

- Start a new test town. Use the actual mouse to choose a cottage, place it, staff a forager and inspect the empty house before its first arrival.
- Move a worker from logging/refining to food and back. Check the available count, retained job/input state, delivery wait and crew maximum. Let a newcomer arrive with no assignment changes.
- Pan, orbit, zoom and select at several elevations, including mined ground. Releasing a camera drag over a panel must stop the drag. Clicking UI must not accidentally pick or build behind it.
- Check Build, Workers, Research, Menu and inspector scrolling at 1440×900, 1366×768 and 1024×768. Smaller phone layouts are not yet a target. Confirm controls, text and the dock remain inside the window, with no transparent panels intercepting world input.
- Save during a foundry reservation and a quarry delivery. Reload and verify crew, housing, research progress, goods and discovery. Test pause and 1×/2×. Leave the application closed and confirm it does not award offline progress.
- Choose Menu [M] → Save & Exit, reopen, and verify the same settlement resumes. In an isolated disposable user-data directory, block the temporary save write and confirm the menu action and window-close request both keep the session open, show an error and retain the previous save. Remove the obstruction and retry. Rendered `--qa` checks the new menu control but deliberately prevents normal-save writes and exit through that action.
- Run the full path in the README without editing game state: a staffed starting economy, gradual population, surveying, discovery, stoneworking/tools, connected quarry, stone delivery, masonry and tower reinforcement. Continue to another useful frontier.
- Exhaust a producer with final cargo travelling. Arm its removal before automatic cleanup, then check that deleting the remaining road needs a fresh confirmation. Occupied housing and the hall must resist removal.
- Import a **copy** of a real Unity save through the file picker. Check retained known terrain, goods, occupied homes, research and cargo. Verify the selected source is unchanged and the prior Godot town is in Previous settlement.
- On disposable saves, test corrupt-primary recovery, an unreadable primary plus unreadable backup, interrupted/rejected writes, New confirmation and the single Previous slot. Do not corrupt the user's real settlement for QA.
- Inspect the scene/log during a sustained developed session. Note frame time and memory, particularly after repeated expansion and loading. A save-boundary test alone is not a large-town performance test.

## Design playtest

Ask the player to establish food, reach stone, reinforce a tower, and then decide how to continue. Observe whether worker allocation creates a choice, whether travel cost is understandable, and whether another discovery seems worth the investment. Record idle time, repetitive hand gathering, reserve depletion and any sense that the game is already finished. Compare at least two seeds; cosmetic variation alone is not strategic replayability. Do not use scripted completion time as measured human playtime.

## Physical devices and export

Select a target device before claiming touch or iOS support. Use the Godot export pipeline and test the produced build on that hardware: tap versus drag, pinch/twist, UI scale/safe areas, scrolling, suspension/resume, local saving, sustained frame time, memory and a full play path. Synthetic gesture events, desktop OpenGL and headless simulation cannot establish those results. No exported build or physical-device pass is implied by this repository's local checks.
