> Historical Unity baseline. The active game is now [Godot First Frontier](../README.md).

# Game design

## Intended experience

Grow a frontier camp into a town on a readable 3D hex island. Citizens move into houses, staff workplaces and eat food; development milestones unlock farming, reforestation and deeper mining. This borrows the settlement-growth concept of Manor Lords while using this project's own rules, hex terrain and artwork.

The current slice is a local town economy. There are no individual family needs, seasons, fertility, markets, taxation, combat, multiplayer or offline production.

## Town loop

1. Four pioneers live at the depot, with 80 timber, 10 metal and 24 food.
2. Build a forager hut and a connected house. Food workplaces receive citizens first.
3. Two citizens move in every 20 seconds while connected housing has space, the last meal was supplied, and food stock covers the incoming population. Arrival consumes 2 food.
4. Grow to 6, 10 and 14 citizens to earn one research point at each milestone. Choose Farming, Reforestation or Deep mining in any order; each costs one point.
5. Connect logging, mining and smelting to produce expansion materials. Build more houses and food production as the town grows.
6. Replant woodland with foresters and develop finite deeper reserves below exhausted surface quarries.

Population is a census, not a family simulation. Four pioneers remain based at the depot; houses each hold four newcomers. Occupied houses cannot be demolished. A disconnected home cannot receive newcomers. Existing residents remain and can work; shortages pause immigration without killing or evicting citizens. Restoring a successful meal permits immigration again.

## Workers and logistics

Each connected, unpaused workplace uses one citizen. Food producers have priority, then other workplaces are assigned in stable coordinate order. Pausing work frees its citizen immediately; existing cargo continues travelling. Hunger does not stop production, preserving a recovery path. Unstaffed or disconnected jobs keep their progress and reserved input.

Output travels to shared depot stock at 0.75 seconds per road edge. Each producer waits for its own delivery before restarting, so shorter routes improve throughput. Buildings also connect roads. Smelters reserve two ore directly from shared stock; there is no separate inbound ore delivery.

Visible citizens are census-sized presentation figures. Their commuting animation follows connected roads, but does not schedule jobs or transfer goods. Cargo animation reflects actual saved shipments; Core alone credits resources. Manual gathering immediately credits stock even without a road.

## Balance

Production times below exclude outbound delivery.

| Item | Cost / rule |
| --- | --- |
| Claim | 6 timber + 1 metal; adjacent owned land only. |
| Road | 2 timber. |
| House | 8 timber; capacity 4 newcomers. |
| Forager hut | 6 timber; 2 food every 8 s. |
| Farm | Farming research; 14 timber + 2 metal; 6 food every 20 s. |
| Logging camp | 10 timber; 1 tree becomes 3 timber in 4 s. |
| Forester | Reforestation research; 12 timber + 2 metal; plants one sapling in 8 s. |
| Surface mine | 14 timber + 2 metal; up to 2 ore every 5 s. |
| Deep mine | Deep mining research; 20 timber + 6 metal; up to 2 deep ore every 7 s. |
| Smelter | 20 timber + 4 metal; 2 reserved ore become 1 metal in 6 s. |
| Meals | Every 30 s; 1 food per 2 citizens, rounded up. A shortage consumes available food and blocks immigration until a successful meal. |
| Immigration | 2 citizens per 20 s of eligible time; needs food equal to population after arrival, vacant connected housing, and no missed-meal flag. Consumes 2 food. Progress pauses during shortages. |
| Hand forage | 2 food from claimed grass/forest; global 8 s cooldown. |
| Hand gather | 3 timber per tree or 1 ore per surface excavation. No deep hand mining. |
| Research | One point at population 6, 10 and 14; each of the three unlocks costs one. |
| Simulation | Fixed 0.125 s steps; foreground time only; pause and 1x/2x speed. |

## Woodland and quarries

The island contains 127 raised hex columns. Surface ore excavation progresses through three depth stages; deep extraction adds three more. Deep reserves start at twice the deposit's original surface ore and never refill on demolition or rebuilding. Deep mines require an exhausted surface deposit. This remains staged excavation, with no arbitrary tunnels, caves or overhangs.

Foresters plant on adjacent claimed grass or forest containing no building or a logging camp, no ore history, fewer than 12 mature trees and no existing sapling. They prefer sites with fewer trees, then logging sites. One sapling per tile matures into one tree after 45 seconds, even if its forester is later removed. Building over a growing sapling is prohibited except for a logging camp with existing trees. Saplings are shown in three growth sizes.

An exhausted logging camp, surface mine or deep mine clears after its final delivery arrives; its access road stays. Hand gathering clears an idle exhausted extractor immediately. A logging camp next to a forester stays to harvest future regrowth. Removing that forester restores normal exhausted-camp cleanup. Smelters and food buildings stay when idle.

Demolition refunds no construction materials. Unprocessed smelter input is returned. The depot, occupied houses, and tiles used by active cargo cannot be removed. Manual removal asks for a second click; automatic exhausted-site cleanup does not.

## Interface and art

The top bar separates stored and travelling timber, ore, metal and food. Town [T] contains population, housing, staffing, meals and building cards; Research [R] shows milestones and unlocks. Select a building, select a site, then Apply or Enter. The inspector offers gathering, hand foraging [F], work pause/resume and guarded removal.

Keys 1–7 retain Inspect, Claim, Road, Logging, Mine, Smelter and Home. New shortcuts are 8 House, 9 Forager, 0 Farm, B Forester and N Deep mine. While Research is open, 1–3 choose its technologies. G centres the next useful site; I toggles inspection; M opens saves; F5/F9 save/load; Escape closes overlays. Mouse/touch camera controls are unchanged.

Guidance starts with food, connected housing and research, then leads through extraction, refining and frontier supply. It never spends resources. New island requires confirmation and archives the current island in one Previous island slot.

World geometry is procedural. The original house, farm and forester PNG illustrations appear in the construction cards; their provenance and exact prompts are in [ART.md](ART.md). They are reference illustrations, not 3D meshes. Touch usability and device performance still require physical-device checks.
