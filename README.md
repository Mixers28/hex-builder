# Alpha Hex Builder

A Unity starter for Project Alpha: build a small industrial settlement, gather timber, mine ore, refine metal, and expand across a raised hex island.

The repository contains the game source, a bootstrap scene, procedural world art, local persistence, a tested economy core, and VS Code configuration. **Unity Editor is still required to import, run, and build the game.** VS Code is the code editor and debugger.

## Start here

1. Extract `alpha-hex-builder.zip` to a writable folder. The enclosed `alpha-hex-builder` folder is already a Git repository on `main`, with an initial commit. There is no hosted remote configured.
2. Install **Unity 6.3 LTS, Editor `6000.3.26f1`**, using [Unity Hub](https://unity.com/download). The exact version is recorded in `ProjectSettings/ProjectVersion.txt`. Add **iOS Build Support** if you intend to export for iPhone or iPad.
3. In Unity Hub, choose **Add project from disk** and select the folder containing `Assets`, `Packages`, and `ProjectSettings`. Open it and allow package import to finish.
4. Initial setup creates the URP renderer, pipeline, and retained shader materials. If setup reports an error, inspect the Console and use **Alpha Hex → Configure project** after resolving it.
5. Choose **Alpha Hex → Open starter scene**, then press **Play**. The island, camera, lighting, and HUD are created at runtime; an empty-looking scene before Play is expected.
6. Open `alpha-hex-builder.code-workspace` in VS Code and accept the recommended **Unity** extension from Microsoft. In Unity's **Preferences / Settings → External Tools**, select Visual Studio Code and regenerate project files. The project includes the **Visual Studio Editor** integration package, which also supports VS Code.
7. In VS Code, use **Run and Debug → Attach to Unity Editor** to debug a running Editor session.

The first import will generate project settings, `Packages/packages-lock.json`, and assets under `Assets/AlphaHex/Settings` and `Assets/AlphaHex/Resources`. Review and commit these files together with their `.meta` files after the first successful import. Unity caches and IDE-generated solution/project files are ignored.

## Your first production chain

You start with a central depot, seven claimed tiles, **80 timber and 10 metal**. Tile coordinates appear in the inspector when selected.

1. Select the forest at **(-1, 0)**. Choose **FORESTRY**, then press the action button in the right-hand inspector. Construction costs 10 timber.
2. Select the ore deposit at **(1, 0)**. Choose **MINE**, then confirm the action. Construction costs 14 timber and 2 metal.
3. Select clear land at **(0, 1)**. Choose **SMELTER**, then confirm the action. Construction costs 20 timber and 4 metal.
4. These three sites touch the depot and are connected immediately. Watch their deliveries reach it. Stock is credited when a shipment arrives; the smelter takes 2 ore from shared stock for each metal job.
5. Select **(0, 2)**, choose **CLAIM**, and confirm. Claim **(1, 2)** next, build a road on **(0, 2)**, and place a smelter on **(1, 2)** if that tile is clear. If it has a resource, gather it or choose another suitable site. The inspector reports whether a building has a route to the depot.
6. Develop the guaranteed frontier forest at **(-2, 0)** or ore deposit at **(2, 0)** when you need more resources. Forests and deposits are finite; extracted ore gradually deepens the quarry.

**Hand gather** immediately supplies 3 timber per tree or 1 ore per excavation on a claimed tile with resources. It also works on a forestry or mine tile, giving you a manual way to get a stalled chain moving.

When a forest or deposit runs out, its forestry camp or mine removes itself after its final shipment reaches the depot. The access road remains, keeping other sites connected and leaving the tile available for construction. Hand gathering the last resource clears an idle extractor immediately. Existing saves receive the same cleanup when simulation resumes. Construction costs are not refunded.

Buildings and road tiles both connect the network. Each producer waits for its own outgoing delivery before beginning another job, so longer routes reduce output. Smelter input is withdrawn from shared depot stock; inbound ore transport is an intentional prototype simplification.

## Controls

| Action | Desktop | Touch |
| --- | --- | --- |
| Select a tile | Left click | Tap |
| Pan | WASD / arrows, or middle drag | One-finger drag |
| Orbit | Q / E, or right drag | Two-finger twist |
| Zoom | Mouse wheel | Pinch |
| Tool | Keys 1–6 or bottom toolbar | Bottom toolbar |
| Return to depot view | 7 / HOME | HOME |
| Apply selected action | Inspector action button | Inspector action button |
| Pause / resume | Space or PAUSE | PAUSE |
| Save / load | F5 / F9 or top buttons | Top buttons |

Tools 1–6 are Inspect, Claim, Road, Forestry, Mine, and Smelter. Escape returns to Inspect. Shift accelerates keyboard panning. The top bar also switches between 1× and 2× simulation speed. Touch controls and phone layout still require physical-device validation.

## What is implemented

- Seeded island with 127 hex coordinates, raised terrain, forest and ore deposits, coastal water, and three excavation depth stages.
- Tile inspection, adjacent land claims, roads, forestry, mines, smelters, and a permanent central depot.
- Procedural trees, rocks, industrial buildings, quarry terraces, and delivery vehicles. No paid art assets are required.
- Resource costs, depletion, connected production, timed deliveries, recipe reservations, and guarded demolition.
- Save, load, 30-second autosave, a fallback save, and a separate previous-island archive. Active production and shipments are serialized.
- Desktop and touch camera controls, selection feedback, a build footprint, and a runtime HUD.
- A Unity-independent C# economy, console checks, Unity EditMode tests, and a GitHub Actions workflow for checks that do not require a Unity license.

**Terrain scope:** this is a raised hex-column world with staged excavation. It does not implement arbitrary voxel tunnels, caves, or overhangs. Multiplayer, combat, trading, power, worker assignment, sound, and offline progression are future work; see the roadmap.

## Saves and new islands

Saves use Unity's `Application.persistentDataPath`, outside the repository. The primary file is `alpha-hex-save.json`; a `.bak` file provides fallback recovery. Unsupported schemas and invalid states are rejected before loading.

**NEW** requires a second click. It archives the current island as `alpha-hex-previous-island.json`, prepares a fresh seed, and saves the new island before switching. **PREVIOUS** restores that archive. Autosaves do not replace the previous-island archive; starting another new island does. There is one archive slot, not a full save browser.

If the primary save cannot be read and no valid fallback exists, the game opens a fresh island with automatic saving disabled for that session until you explicitly save or start a new island. This avoids silently overwriting the unreadable file. Elapsed time while the application is closed does not generate resources.

## Work from VS Code

Use **Terminal → Run Task** for the included tasks, or run these commands in the repository root:

```sh
# Optional: install the .NET 10 SDK for the standalone economy checks.
dotnet run --project tools/SimulationChecks/SimulationChecks.csproj --configuration Release

# Optional Python 3 helper: validate repository files and Unity GUID references.
python3 tools/validate_repo.py

# Open the pinned Unity Editor, discovered in standard Unity Hub locations.
python3 tools/unity_cli.py open
```

On Windows, use `python` if `python3` is not available. `global.json` selects .NET 10 for the console checks only; the Unity source uses Unity-compatible C# 9 and does not depend on the .NET 10 runtime.

To run Unity EditMode tests, use **Window → General → Test Runner → EditMode → Run All**, or close the interactive Editor for this project and run:

```sh
python3 tools/unity_cli.py test
```

The helper also accepts `configure`, `build-macos`, `build-windows`, and `build-ios`. Set `UNITY_EDITOR` to the full Unity executable path, or pass `--editor`, if discovery does not match your installation. Batch logs and test results go to ignored `Artifacts/`.

### Verification when this starter was prepared

Subsequent local validation passed 19 Core checks, 7 Unity EditMode tests, and an Editor Play Mode check of automatic extractor cleanup. See the latest entry in the [verification record](docs/VERIFICATION.md) for scope and remaining checks.

**15 standalone simulation checks passed** against the same Core source used by Unity. They cover deterministic generation, valid and invalid actions, finite resources, road connectivity, shipments, refining reservations, demolition, production accounting, save continuity, timestep consistency, and malformed states.

Unity Editor is not installed in the preparation environment. **Unity compilation, package import, scene rendering, Unity EditMode tests, and iOS builds have not been executed.** The five Unity EditMode tests include the real Unity JSON serialization path. See the [verification record](docs/VERIFICATION.md) and run the checklist in [docs/QA.md](docs/QA.md) on the first Editor import; a console test pass does not establish a Unity or device pass.

## Build for iOS

On a Mac, install Xcode and the pinned Editor's iOS Build Support module. After the Editor and touch checks pass, choose **Alpha Hex → Build → iOS Xcode project**, or run `python3 tools/unity_cli.py build-ios` with the interactive Editor closed.

Open the generated Xcode project under `Builds/iOS`, choose your signing team and a unique bundle identifier, then build and run on a device. The current placeholder identifier is `com.projectalpha.hexbuilder`; change it in `ProjectSetup.cs` before exporting so automatic setup does not restore the placeholder. Signing identities are local configuration and are not included. These build commands currently produce development builds.

## Publish this repo to GitHub

The ZIP includes its `.git` directory and initial commit, so you can continue from that history. To create a private remote, open the extracted repository in VS Code and use **Source Control → Publish Branch**, select your GitHub account, then **Publish to GitHub private repository**.

If you use GitHub CLI and are signed into `Mixers28`, the equivalent command is:

```sh
gh repo create Mixers28/alpha-hex-builder --private --source=. --remote=origin --push
```

For an already-created empty remote, use:

```sh
git remote add origin https://github.com/Mixers28/alpha-hex-builder.git
git push -u origin main
```

Do not create a second initial commit when publishing; the starter already has one. The provided workflow runs structure and console checks on pushes to `main` and pull requests. It does not run Unity builds.

## Where to change things

| Path | Responsibility |
| --- | --- |
| `Assets/AlphaHex/Scripts/Core/Simulation.cs` | Economy, generation, action validation, routing, production, and state validation. |
| `Assets/AlphaHex/Scripts/Core/WorldState.cs` | Serializable save data. |
| `Assets/AlphaHex/Scripts/Runtime/GameBootstrap.cs` | Session, input routing, tools, objectives, and save actions. |
| `Assets/AlphaHex/Scripts/Runtime/GameHud.cs` | HUD layout and player-facing actions. |
| `Assets/AlphaHex/Scripts/Runtime/HexWorldView.cs` | Tile refresh and shipment visuals. |
| `Assets/AlphaHex/Scripts/Runtime/WorldArt.cs` | Procedural geometry, palette, and building silhouettes. |
| `Assets/AlphaHex/Scripts/Runtime/OrbitCamera.cs` | Mouse, keyboard, and touch camera. |
| `Assets/AlphaHex/Editor/ProjectSetup.cs` | Initial render setup, application settings, and build entry points. |
| `tools/SimulationChecks` | Fast tests against the actual Core files. |

Read [architecture](docs/ARCHITECTURE.md), [game design and balance](docs/DESIGN.md), [roadmap](docs/ROADMAP.md), and [QA](docs/QA.md) before expanding a system. `AGENTS.md` gives coding assistants the same project boundaries.

## References

- [Unity 6 releases](https://unity.com/releases/unity-6)
- [Official VS Code Unity setup](https://code.visualstudio.com/docs/other/unity)
- [Unity iOS environment setup](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-environment-setup.html)
- [Original hex-island reference shared for this direction](https://www.reddit.com/r/aigamedev/comments/1x0xkr2/working_on_a_hexa_island_builder_with_opus/)

This repository contains original prototype code and procedural geometry. No code or art was copied from the Reddit reference.
