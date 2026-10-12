# Game design · Alpha Hex

## Intended experience and agreed direction

Develop a small frontier village by supporting gradual immigration, choosing where people work, and turning discoveries into productive capabilities. The user wants a visually richer, replayable settlement that grows incrementally across weeks. Their 2026-10-10 playtest found the earlier Unity loop enjoyable and understandable but shallow and effectively exhausted after about fifteen minutes.

The active engine is **Unity 6.3 LTS (6000.3.26f1)** following the user’s 2026-10-11 approval to return to Unity. Slice 002 carries forward the Godot frontier and replaces its circular metal dependency with clay, bricks and primitive smelting. It is groundwork for the longer game, not evidence of weeks of completed content. See the [decision log](DECISIONS.md) and [current slice](slices/002-clay-and-first-metal.md). `UNITY_DESIGN.md` and Slice 001 describe historical versions.

## Long-term progression vision

The user's expanded direction on 2026-10-10 connects settlement management, equipment, military defense and seafaring. These are agreed destinations for the game; their detailed mechanics and slice order remain to be designed. They are not implemented by the current First Frontier build.

| Pillar | Agreed direction |
| --- | --- |
| Tools and equipment | Research better tools to improve work efficiency and gain the capability to gather better resources. Equipment progression should give new discoveries a practical use. |
| Refining | Research better refinement methods so improved materials can become better equipment. Gathering, processing and equipment capabilities advance together. |
| Weapons and armies | Research weapons and raise armies to defend the settlement against roving barbarians. Military capability belongs to the wider production economy. |
| Rival settlements | Other villages compete for resources, giving territorial expansion and defense a purpose beyond increasing the settlement's size. |
| Islands and boats | Exploration eventually reveals that the starting settlement is on an island. Build boats to reach further land, then research better boats and equipment to access further resources. |
| Animals | Introduce animals as sources of meat, leather and related production opportunities. The first animal types and acquisition/processing methods remain open. |

The intended recurring loop is: support a settlement → discover resources → research how to gather and refine them → make better equipment → expand productive and defensive capabilities → reach new territory and repeat with new constraints. Military preparation, productive upgrades and seafaring should create competing uses for the economy. The goal is meaningful decisions across repeated sessions, with visible village growth and reasons to return.

For planning, rival villages and barbarians are assumed to be computer-controlled inhabitants of the single-player world. Army control, recruitment and casualties, rival behavior, equipment assignment or wear, hunting versus husbandry, boat roles and travel, and exact resource tiers remain open. Naval combat, diplomacy and multiplayer have not been selected. Active-play-only progress remains the current rule.

The [roadmap](ROADMAP.md) proposes an incremental order. Each new slice should demonstrate a complete production or expansion payoff and preserve existing saves. Weeks of engagement will require playtesting these interacting systems; content count and longer research timers alone cannot establish it. The current radius-16 map does not yet model a complete island or travel between islands.

## Next opening: clay and first metal

The user accepted the recommended manual, worker-operated primitive batch and a 45–60-minute human playtest target. Timber is the first fuel; clay and brick are the only new resource counters. Low-quality smelting means slower, less efficient processing, not a second metal quality inventory.

New villages begin with four pioneers, seven claims, 80 timber, 24 food and **zero ore, metal, stone, clay and bricks**. A guaranteed clay bed at `(0,-2)` lies within the initial radius-three discovery. Claims cost **6 timber**, roads **2 timber**. Neither requires metal. Manual timber, ore, clay and food recovery remains available on owned suitable land.

The path is food/housing/wood → clay → researched brick firing → kiln → researched primitive smelting → manually started bloomery → researched efficient refining → automated foundry → surveying and stone. Materials are paid at construction or research start. In-flight goods are not spendable.

## Housing, migration and crews

- Four pioneers live at the hall. Each cottage adds four places and starts empty.
- With connected vacancies, a sufficient food reserve and no missed last meal, one newcomer arrives every 90 simulated seconds and consumes two food. Housing fills automatically; jobs do not.
- Every 30 seconds the village eats `ceil(population / 2)` food. A shortage pauses supported immigration; hand foraging gives two food with an eight-second cooldown.
- Explicit crews have a speed multiplier of 1.0 / 1.55 / 2.10 for one / two / three workers. Total assignments cannot exceed citizens. Pausing releases the crew and retains valid work and inputs.
- A producer shares one cart across its crew and waits for delivery before starting another job. Longer roads still reduce throughput.

## Construction and production balance

Timings below are base job durations with one worker, before delivery. Cottage and tower functions do not require crews.

| Building | Construction | Crew cap | Output / function |
| --- | --- | --- | --- |
| Cottage | 10 timber | 0 | Four housing places |
| Forager lodge | 8 timber | 2 | 3 food / 12 s |
| Woodcutter | 12 timber | 3 | 1 tree → 3 timber / 12 s |
| Clay pit | 12 timber | 3 | Up to 3 finite clay / 24 s |
| Brick kiln | 16 timber, 6 clay | 3 | 4 clay + 2 timber → 2 bricks / 45 s |
| Primitive bloomery | 16 timber, 8 bricks | 1 | Manual batch: 3 ore + 3 timber → 1 metal / 90 s |
| Iron mine | 16 timber, 6 bricks | 3 | Up to 2 finite ore / 15 s |
| Foundry | 24 timber, 4 metal, 16 bricks | 3 | 2 ore + 1 timber → 1 metal / 18 s, automatic |
| Farm | 18 timber, 6 bricks | 3 | 6 food / 24 s |
| Tree nursery | 18 timber, 4 clay | 2 | Plant an adjacent sapling / 16 s; mature after 90 s |
| Watchtower | 24 timber, 4 metal | 0 | Connected radius-four survey |
| Stone quarry | 22 timber, 6 metal | 3 | Up to 2 finite stone / 20 s |
| Deep mine | 26 timber, 8 metal | 3 | Up to 2 finite deep ore / 18 s |

Each bloomery batch requires a separate **Start batch** action and one assigned worker. It reserves all inputs immediately; it cannot queue or repeat itself. A foundry is faster and uses less ore and timber per metal. Clay pits, kilns, mines and foundries repeat their jobs while staffed, connected and supplied.

Nurseries plant only suitable adjacent owned grass or woodland, avoiding buildings and mineral history. Woodland capacity is 18 trees, matching the upper range of generated starting forest. Managed logging stays for regrowth. Reserve planned construction sites before allowing them to be replanted.

## Research

One project runs at a time. Materials are paid once; progress survives saves. Population gates are checked at start. The original three technologies may use retained legacy milestone credits in imported settlements.

| Research | Citizens | Materials | Time | Prerequisite / payoff |
| --- | --- | --- | --- | --- |
| Brick firing | 6 | 20 timber, 8 clay | 180 s | Unlock kilns |
| Primitive smelting | 8 | 24 timber, 12 bricks | 240 s | Brick firing; unlock bloomeries and iron mines |
| Efficient refining | 10 | 24 timber, 2 metal, 10 bricks | 300 s | Primitive smelting; unlock foundries |
| Cultivation | 6 | 16 timber, 6 bricks | 90 s | Unlock farms |
| Woodland stewardship | 6 | 18 timber, 4 clay | 120 s | Unlock nurseries before refined metal |
| Frontier surveying | 6 | 16 timber, 3 metal | 90 s | Unlock watchtowers |
| Stoneworking | 6 | 12 timber, 4 metal | 90 s | Surveying and discovered stone |
| Quarrying tools | 8 | 24 timber, 8 metal | 150 s | Stoneworking; unlock stone quarries |
| Frontier masonry | 10 | 20 timber, 6 metal, 10 stone | 180 s | Quarrying tools; unlock tower reinforcement |
| Deep mining | 10 | 30 timber, 8 metal | 180 s | Unlock deep mines on exhausted surface deposits |

Reinforcing a wooden watchtower costs 12 timber, 4 metal and 16 stone. A connected reinforced tower reveals radius six. Discovery does not grant ownership or resources and remains visible after tower removal.

## Finite resources, frontier and persistence

Only 37 hexes initially exist in the simulation. Further discovery generates seeded terrain within radius 16 (817 possible coordinates). Hex radius is 2.35; mist hides unknown land without exposing its resources or maximum boundary. The current map is not a completed island/seafaring system.

Trees, surface ore, deep ore, stone and clay are finite. A tree yields three timber; hand mining/gathering yields one ore/clay. Surface and deep extraction have three visual stages each; stone has three. A deep ore reserve begins at twice its surface deposit. Saplings provide renewable wood when researched and supported; manual gathering never refills a depleted deposit.

Carts take 1.25 seconds per road edge; imported older 0.75-second trips finish unchanged. Stores, reserved recipe inputs and cargo are accounted separately. Demolition refunds unused reserved inputs once, not construction costs, and refuses the hall, occupied homes and active delivery routes. Exhausted extractors clear after final delivery while retaining their road.

Unity schema 4 imports Unity schemas 1/2 and Godot schema 3 without rewriting the source. Existing industry is grandfathered through the new gates. A previously reserved ore-only foundry job finishes on its original inputs before adopting the new recipe. See [architecture](ARCHITECTURE.md) for validation, backup, import and quit behavior.

## Pacing and boundaries

The current command-only benchmark reaches first metal in 18.2 simulated minutes and a working foundry in 31.4. It uses hand gathering and informed choices and is not a human playtime estimate. Test the 45–60-minute target with friends, recording idle waiting, unclear recipes and useful labour decisions. Do not achieve the weeks-long ambition merely by stretching timers.

There is no victory screen, offline progress, manufactured tool inventory, charcoal/coal, metal grades, weapons, combat, animals, boats, competing-village AI, multiplayer or arbitrary tunnelling in this slice. Those destinations remain in the roadmap and require their own agreed playable milestones.
