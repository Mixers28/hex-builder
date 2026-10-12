# Slice 002 · Clay and First Metal

Date: 2026-10-11. Status: accepted and implemented in Unity; runtime/distribution verification is recorded separately in VERIFICATION.md.

## Playtest evidence and purpose

The player reports that progression is too fast and that metal can block further construction. In the reported Godot 0.1 build, the foundry costs 20 timber and 4 metal; each claim costs 6 timber and 1 metal. A fresh village can spend all 10 starting metal on legal claims, gather two ore by hand, and still have 20 timber, yet cannot build a foundry or mine. The resulting save record passes validation. Hand gathering offers no ore-to-metal conversion. See [the diagnostic evidence](../VERIFICATION.md#metal-recovery-diagnosis--2026-10-11).

Test whether a recoverable opening with useful clay, brick and primitive-smelting stages produces more interesting worker and investment choices before efficient iron industry. This is one early progression slice, not the complete weeks-long game.

## Agreed direction and accepted defaults

Agreed from the user: primitive manual smelting, clay and bricks before iron-dependent industry, a slower/layered opening, and reference research into comparable builders. Return active development to Unity 6.3 LTS and preserve gradual housing occupancy, explicit staffing, active-play-only progress, fog discovery and save continuity.

| Question | Recommended answer | Alternative |
| --- | --- | --- |
| How does a primitive smelting job begin? | The player starts one timed batch; one assigned worker performs it. | One assigned worker repeats batches automatically. |
| How long should first useful iron production take in a fresh village? | Approximately 45–60 minutes of active play, subject to playtesting. | Approximately 2–3 hours of active play. |

The user accepted both recommended answers with “proceed as recommended.” Exact implemented costs and timings are in DESIGN.md. The human timing remains a playtest target, not a measured result.

## Accepted playable path

| Stage | Player action and payoff | Constraint |
| --- | --- | --- |
| Establish the village | Build housing and food/wood production; allocate the initial workers. | Basic survival and recovery require no refined metal. |
| Gather clay | Reach a guaranteed early clay source; gather by hand, then invest in a clay pit. | Clay access, claims and extraction must be possible without metal. |
| Fire bricks | Use a simple kiln to process clay with timber fuel. Bricks fund the primitive smelter and later industry. | The kiln itself cannot require fired bricks or iron to bootstrap the chain. |
| Make first metal | Build a low-output primitive smelter from early materials. Process hand-gathered ore with timber fuel. | Building and research prerequisites contain no refined metal. Batch input is reserved once. |
| Improve refining | Invest earned metal and bricks in the foundry for a clear throughput or fuel-efficiency benefit. | Keep the primitive recovery option available; do not require a reset after spending metal. |
| Rejoin the frontier | Put delivered metal into the existing surveying, stone and watchtower progression. | Retain its logistics and discoveries; rebalance the opening into it. |

Use only **clay** and **brick** as new resource counters. Existing timber provides the initial fuel. “Low quality” means a less efficient process, not a second metal-quality inventory. New villages start without refined metal; existing settlements retain their stock. The first command-driven route reaches metal in 18.2 simulated minutes and an operating foundry in 31.4. Human pacing still needs measurement.

The UI should show each building's inputs, output, batch status, assigned worker, delivery wait and reason for being blocked. Make the primitive smelter, kiln and later foundry visually distinct using the existing procedural art. Preserve readable fullscreen controls as the resource strip and build menu grow. No external assets are needed.

## Design references

Manor Lords separates extraction, processing and manufacture: its clay becomes roof tiles, and iron ore becomes iron slabs before manufactured products. This supports borrowing distinct industrial jobs and labour trade-offs; it does **not** establish a mandatory clay-brick-before-iron sequence for our game. Our brick progression is a separate design choice. [Official resource reference](https://wiki.hoodedhorse.com/Manor_Lords/Resources/en).

Factorio identifies the stone furnace as its cheap basic smelter, with more capable furnaces later. The applicable lesson is to preserve an accessible first processing tier and give its replacement a measurable benefit. [Official stone furnace reference](https://wiki.factorio.com/Stone_furnace).

## Preservation and acceptance

- A new zero-metal village with available wood, clay and surface ore can make metal and build a foundry using normal commands, without direct grants. Audit the complete dependency chain, including claims, roads, research and staffing.
- A valid existing village that spent its starting metal can recover without losing its town. Existing discovered terrain and depleted deposits must not be regenerated. Specify how old worlds acquire clay without overwriting known land before migration is implemented.
- Preserve stored, reserved and travelling resources exactly once across production, crew changes, pause, removal, save/load and interrupted jobs. Extend schema validation deliberately for additional inputs and manual jobs; current foundry reservations store ore only.
- Existing foundries, technology, crews, buildings, population and shipments remain usable after migration. Do not require old players to rebuild their settlement or repay completed research.
- A producer still waits for its own delivery. Timber used as fuel competes with construction and renewal; it is not an unlimited background grant.
- Test the first-metal path across seeds, the reproduced zero-metal failure, failed commands without partial spending, and save continuity at each production stage. Keep finite-resource exhaustion distinct from a circular recipe dependency; no claim of recovery after all raw materials are exhausted.
- Run the actual C# simulation verifier, Unity JsonUtility tests, rendered player smoke and fullscreen layout checks. Rebuild the complete Windows package after those pass, and test the actual EXE on Windows before claiming target-platform verification.
- Human playtests measure time to first metal, time waiting without a useful choice, understanding of recipes and whether the foundry investment feels worthwhile. A passing scripted route proves feasibility, not enjoyable pacing.

## Boundaries and delivery

Keep charcoal/coal, copper/tin, separate metal grades, durability, manufactured equipment, combat, animals, ships and new household needs for later slices. The existing long-term vision remains in [the roadmap](../ROADMAP.md). This slice should establish a complete early production loop before adding those systems.

Deliver updated design/balance constants and decision log, compatible saves, a complete Unity project ZIP, a fresh Windows playtest ZIP, key-moment captures, a short start-to-finish guide and clear automated versus human/device test evidence. Publisher signing is a separate distribution task: see [Windows builds](../WINDOWS_BUILD.md#publisher-trust-and-signing).
