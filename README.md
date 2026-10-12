# Alpha Hex · Clay and First Metal

A Unity settlement builder about people, production and a growing hex frontier. **Unity 6.3 LTS, pinned to 6000.3.26f1**, is the active engine again. Open this repository root in Unity and play `Assets/Scenes/Starter.unity`.

The new opening starts with **zero metal**. Clay becomes bricks; a worker-operated bloomery makes the first metal; an efficient foundry automates refining. Citizens move into cottages gradually, workers are assigned manually, and connected watchtowers reveal new land. All world models are generated in code.

The previously released Godot demo is preserved in [`godot/`](godot/README.md). [GitHub Releases](https://github.com/Mixers28/hex-builder/releases) contains published playtests; the older `v0.1.0-playtest.1` ZIP is the Godot version and has no updater. See [Windows distribution](docs/WINDOWS_BUILD.md) for the Unity installer, release procedure and signing status.

## Play the opening

1. Build a **cottage** at `(0,1)` and a **forager lodge** at `(-1,1)`. Assign two foragers. Build a woodcutter at `(-1,0)` and assign one worker. Cottages start empty; a supported newcomer arrives every 90 simulated seconds.
2. Claim the clay bed at `(0,-2)` for **6 timber**. Road `(0,-1)` and build a clay pit. Assign the fourth worker. You can also gather clay, timber and surface ore by hand on owned land.
3. At six citizens, research **Brick firing**. Build a kiln at `(1,-1)` and assign a worker: **4 clay + 2 timber → 2 bricks**. Consider Woodland stewardship and a tree nursery before the nearby forests run out. Place planned buildings before letting a nursery replant their plots.
4. Add a connected cottage at `(0,2)`. At eight citizens, research **Primitive smelting** using bricks. Build a bloomery on connected clear land, assign one worker, and choose **Start batch**: **3 ore + 3 timber → 1 metal**. Each batch takes 90 seconds plus its cart trip. Hand mining supplies ore before a mine is built.
5. Produce at least six metal: two for **Efficient refining** research at ten citizens and four for the foundry. The foundry also costs 24 timber and 16 bricks. It automatically processes **2 ore + 1 timber → 1 metal** in 18 seconds with one worker, plus delivery.
6. Research surveying, build a connected watchtower, discover stone, then research stoneworking and quarry tools. Staff a stone quarry. Frontier masonry and 12 timber / 4 metal / 16 stone reinforce a watchtower and reveal more land.

Crews share one cart per workplace and wait for it to return. Shorter roads and better worker allocation improve throughput. Production pauses when no crew, inputs, capacity or connection is available. Timed progress happens only while the game is running.

A command-driven route reaches first metal in **18.2 simulated minutes** and a working foundry in **31.4 minutes**. This proves feasibility, not human playtime. The 45–60-minute human target needs playtesting; weeks of meaningful progression remain the longer-term ambition.

## Controls and saves

| Action | Control |
| --- | --- |
| Select / inspect | Left click |
| Buildings / workers / research | B / T / R |
| Inspect / claim / road | 1 / 2 / 3 |
| Apply selected tool | Enter or inspector button |
| Pan / orbit / zoom | WASD or arrows / Q–E or right drag / wheel |
| Pan by mouse | Middle drag |
| Return to village | 7 |
| Pause / speed | Space / top-bar speed button |
| Emergency food | F on owned grass or forest |
| Save / load / menu | F5 / F9 / M |
| Fullscreen | F11 |
| Close a panel | Escape |

Use **Menu → Save & Exit**. If the save fails, the game stays open. Normal window closure also saves when automatic saving is enabled. Automatic saves run every 30 seconds. A backup and one previous-settlement slot protect recovery and explicit replacements.

Unity saves live under the existing `Project Alpha / Alpha Hex Builder` persistent-data directory, outside the installation. **Menu → Import Godot save** reads the former `AlphaHexFrontier/frontier.json` file, including its backup fallback. You can edit the import path. Import archives the current village and leaves the original source untouched. Unity schemas 1/2 and Godot schema 3 migrate to Unity schema 4.

On installed Windows builds, **Menu → Check for updates** checks published GitHub Releases. Download while playing, then choose **Save, update & restart**. A one-time Unity installation is needed for friends using the older Godot ZIP. The updater does not sign the game or remove Windows reputation warnings.

## Development and verification

- [`docs/DESIGN.md`](docs/DESIGN.md): current rules, balance and future boundaries.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md): Core, runtime, persistence and update protocol.
- [`docs/DECISIONS.md`](docs/DECISIONS.md): engine choice and design decisions.
- [`docs/VERIFICATION.md`](docs/VERIFICATION.md): actual automated and platform evidence.
- [`docs/slices/002-clay-and-first-metal.md`](docs/slices/002-clay-and-first-metal.md): accepted scope and playtest questions.

```sh
dotnet run --project tools/SimulationChecks/SimulationChecks.csproj --configuration Release
python3 tools/validate_repo.py
python3 tools/unity_cli.py test
python3 tools/unity_cli.py build-macos
python3 tools/unity_cli.py build-windows
```

The console verifier uses .NET 10 and links the actual Core source; Core itself remains C# 9 / .NET Standard 2.1. Unity tests cover real `JsonUtility`. Windows builds need the pinned editor's Windows Mono support module. The [distribution guide](docs/WINDOWS_BUILD.md) explains packaging and releases.

This is a bounded single-player slice: no armies, competing villages, boats, animals, multiplayer, arbitrary tunnels or offline production yet. Terrain supports layered hex columns and staged quarry excavation. The [roadmap](docs/ROADMAP.md) separates the planned larger game from implemented features.
