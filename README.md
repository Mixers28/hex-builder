# Alpha Hex · First Frontier

A playable **Godot 4.7.2** settlement builder: welcome people gradually, assign production crews, reveal a seeded frontier, research quarry tools, and use stone to expand again.

For friends on Windows: [download the Windows x64 playtest ZIP](Artifacts/alpha-hex-first-frontier-windows-x64.zip), extract it, and run `AlphaHex.exe` from the `AlphaHex` folder. Keep `AlphaHex.pck` beside it. The package includes a play guide and needs no Godot installation. Native Windows launch/performance remains to be tested on a PC; see the [Windows build and playtest notes](docs/WINDOWS_BUILD.md).

Open **[godot/project.godot](godot/project.godot)** in the standard Godot editor and press **F5**. No .NET edition, external assets or package installation is needed. A [complete standalone Godot ZIP](Artifacts/alpha-hex-first-frontier-godot.zip) is also available locally. The previous Unity project is preserved at the repository root; its [historical instructions](docs/UNITY_BASELINE.md) are separate from this active game.

## Play the slice

Start with four pioneers, seven owned plots, 80 timber, 10 metal and 24 food. Only 37 hexes are visible. Select a plot to inspect it; choose **Build [B]**, a building, and the inspector's build action. **Workers [T]** and the inspector assign crews.

1. Build a **cottage** at `(0,1)` and a **forager lodge** at `(-1,1)`. Give the lodge one worker. Houses begin empty; a supported newcomer arrives every 90 simulated seconds and occupies a home automatically.
2. Build a **woodcutter** at `(-1,0)`, **iron mine** at `(1,0)`, and **foundry** at `(1,-1)`. Assign one worker to each. These sites touch the village hall. Goods become spendable when their carts arrive.
3. Add a second cottage, for example at `(0,-1)`. Allocate newcomers yourself. Keep food supplied and move crews to the materials you need. Deposits run out; preserve **6 timber and 1 metal** for another resource claim. Hand gathering on owned woodland or iron is available for recovery.
4. At six citizens, research **Frontier surveying**. Claim `(0,2)` and build a watchtower connected through the cottage at `(0,1)`. It reveals more land, including stone at `(0,5)`; it does not claim that land.
5. Research **Stoneworking**, then **Quarrying tools** at eight citizens. Claim `(0,3)`, `(0,4)`, and `(0,5)`; lay roads on the first two and build a **stone quarry** on the third. Assign its crew and follow its stone cart back to the hall.
6. At ten citizens, fund **Frontier masonry** with the delivered stone. Reinforce the watchtower for **12 timber, 4 metal and 16 stone** to reveal the next frontier. Save and continue expanding. Farms, tree nurseries and deep mines offer supporting research choices.

There is no victory screen. This build provides one new resource chain and further expansion within a radius-16 limit (817 possible coordinates). A scripted, economical route reached the first reinforced tower in **15.6 simulated minutes** using hand gathering and 115 validated commands; that is not a human playtime estimate. Weeks of meaningful progression remain a content and playtesting goal, not a completed feature.

The agreed [long-term direction](docs/DESIGN.md#long-term-progression-vision) adds better tools and refining, weapons and armies, barbarians and competing villages, animals for meat and leather, and eventual island exploration with boats. These are future systems; the [roadmap](docs/ROADMAP.md) proposes their slice order.

## Controls

| Action | Control |
| --- | --- |
| Select a plot | Left click |
| Inspect / claim / road | 1 / 2 / 3, then inspector action or Enter |
| Build / workers / research | B / T / R |
| Pan | WASD, arrows, or middle-button drag |
| Orbit | Q / E or right-button drag |
| Zoom | Wheel or trackpad pinch |
| Home view | Home / 7 / house icon |
| Pause / speed | Space; top-bar 1× / 2× |
| Emergency food | F on owned grass or woodland; eight-second cooldown |
| Save / load / menu | F5 / F9 / M |
| Save and exit | M → Save & Exit |
| Close panel | Escape |

Touch tap, drag, pinch and twist handlers are present. Desktop rendering and synthesized gestures were checked; physical touch devices and iOS exports were not tested. The current UI targets landscape desktop windows at least 1024×700. It scales from a 1440×900 reference layout, keeping text and buttons readable in Retina fullscreen while the 3D world renders at the window's native resolution. Restart an already-running game to pick up display configuration changes.

## Saves

Choose **Menu [M] → Save & Exit** to save the current settlement and close the game. The next launch resumes that save. If saving fails, the error is shown and the game stays open so you can retry. Normal window closure also waits for a successful save when autosaving is enabled.

Time advances only during play. Autosave runs every 30 seconds. Native saves use Godot's user-data directory `AlphaHexFrontier`: `frontier.json`, its `.bak` fallback, and one `previous.json` settlement archive. On macOS this is `~/Library/Application Support/AlphaHexFrontier/`.

**Menu → Begin a new settlement** asks for confirmation and archives the current village before replacing it. **Import Unity save…** accepts the older `alpha-hex-save.json` schema 1/2 files, preserves their known world, town, resources, job progress and travelling cargo, and leaves the selected source file unchanged. Import also archives the current Godot village. Old unspent research points remain usable for their original technologies. Invalid saves cannot partially replace the active town; an unreadable startup save pauses autosave until an explicit successful save or replacement.

## Verification and development

Use the executable for **Godot 4.7.2** (`godot` below) from the repository root:

```sh
godot --path godot --headless --editor --import --quit
godot --path godot --headless --script res://tests/simulation_checks.gd
godot --path godot -- --qa
godot --path godot -- --qa --qa-display
python3 tools/validate_repo.py
```

The rendered commands open a separate game window and write screenshots to ignored `godot/qa-output/`. `--qa` exercises UI/input and the frontier scenario. Adding `--qa-display` instead tests native fullscreen, Retina button dimensions, window resizing and scaled pointer interactions, then exits. Neither loads or overwrites the normal settlement save. The engine's exit code and log must both be checked for script errors.

Rules live in `godot/core`; scene, meshes, input and UI live in `godot/runtime`. Tests preload those actual rules. Keep `.uid` files, exclude `.godot` and private saves. The existing C# verifier and Unity CI belong to the preserved baseline; they do not verify the Godot game.

See [design and balance](docs/DESIGN.md), [architecture](docs/ARCHITECTURE.md), [slice and acceptance record](docs/slices/001-first-frontier.md), [verification](docs/VERIFICATION.md), and [decisions](docs/DECISIONS.md). No combat, multiplayer, arbitrary tunnels, tool durability, audio, offline production or additional resource tiers were added.
