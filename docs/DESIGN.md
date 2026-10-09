# Game design

## Intended experience

Build a small industrial settlement on a readable 3D hex landscape. Gather resources, establish extraction, refine ore, connect production, and expand into new deposits. The landscape should visibly change as the player works it.

The current focus is construction and economic growth. PvP, combat, multiplayer, and offline background production are not implemented.

## World and mining

A new session creates a seeded world with a radius of six hexes. The starting base and surrounding radius-one territory are claimed. Expansion develops adjacent territory on that existing map; it does not create new islands or an infinite world.

Terrain is represented by raised hex columns. Forests and mineral deposits provide opportunities for production. Quarry depth progresses from zero to three in proportion to the original ore being extracted. Depleted ore tiles become rock; depleted forest tiles become grass.

This representation supports elevation, deposits, depletion, and a visibly deepening quarry. It does not support arbitrary underground tunnels, caves, overhangs, or freeform voxel editing. Those features would require a separate terrain and interaction design decision.

## Playable loop

1. Inspect the starting territory and identify nearby timber and ore.
2. Gather starting resources manually through the available actions.
3. Build forestry or a mine to establish recurring extraction.
4. Connect production to the depot through roads or connected building tiles.
5. Build a smelter and convert delivered ore into refined metal.
6. Use accumulated construction resources to claim the frontier and establish another production site.

The first useful session goal is a working extraction-to-refining chain followed by one expansion that reaches a new opportunity. Numeric costs, yields, and durations are prototype balance values in Core, not a finalized economy.

## Production roles

| Element | Purpose |
| --- | --- |
| Manual gathering | Provide immediate interaction and help bootstrap the first production chain. |
| Forestry | Produce timber from a suitable forest tile. |
| Mine | Extract ore from a suitable deposit and advance its quarry state. |
| Smelter | Convert ore from connected shared stock into refined output. |
| Depot | Provide the shared stock destination for the production network. |
| Roads | Connect sites and determine shipment routes. |
| Frontier claims | Make additional adjacent land available for development. |

## What logistics currently means

Production buildings dispatch outbound goods along the road/building network. Goods take time to arrive at the depot and become available in shared stock. Each building waits for its own shipment to arrive before starting another production cycle, making distance a continuing throughput cost. The player should be able to distinguish a resource still travelling from one available to spend.

Smelter inputs are simplified: once connected, the smelter reserves ore from shared stock without an inbound carrier journey. Manual gathering grants stock immediately, even on a disconnected tile: harvesting consumes one tree for three timber, and excavation consumes one ore for one ore in stock. These explicit player actions are not a complete worker simulation.

The current system should not be described as a finished supply-chain game. Dedicated inbound delivery, assigned workers, fuel, power, congestion, transport upgrades, and multiple storage destinations need separate implementation and balancing.

## Initial balance

These are starter values for tuning. Core is the source of truth if implementation values change.

| Item | Starting value or rule |
| --- | --- |
| Initial depot stock | 80 timber, 0 ore, 10 metal. |
| Frontier claim | 6 timber and 1 metal. |
| Road | 2 timber. |
| Forestry | 10 timber to build; one tree produces 3 timber in 4 seconds. |
| Mine | 14 timber and 2 metal to build; produces up to 2 ore in 5 seconds. |
| Smelter | 20 timber and 4 metal to build; reserves 2 ore and produces 1 metal in 6 seconds. |
| Shipment travel | 0.75 seconds per connected hex edge. |
| Producer restart | Waits until its current outbound shipment arrives. |
| Demolition | No construction refund; reserved, unprocessed ore is returned where relevant. The depot and active shipment route tiles cannot be removed. |

Production times exclude delivery. With enough ore, a smelter four edges from the depot needs a 6-second job and a 3-second delivery before restarting. Its nominal cycle is therefore 9 seconds, subject to the simulation's time-step handling. This makes the cost of a distant site inspectable without adding a separate fleet-management system.

## Decisions to test

Removing attacks leaves space for economic choices. The first playtests should establish whether those choices are already understandable and consequential:

- Does a nearby deposit justify developing it before a more distant one?
- Does connecting a new production building noticeably improve useful output?
- Can players identify whether extraction, refining, or transit is delaying expansion?
- Does using resources for another building feel different from spending them to claim land?
- Does a depleted forest or deepened quarry make the industrial site feel changed?

The one-shipment-per-producer rule gives the prototype a simple transport constraint. Playtest whether this creates enough placement choice before adding carrier fleets, congestion, or local stock limits. Avoid adding several new resource systems to compensate for an unclear core loop.

## Presentation and controls

The world uses an angled orbit camera and tile selection. Building and mining actions should operate on the selected hex with clear eligibility and failure feedback. The HUD should expose stock, selection details, production status, and available actions without requiring the player to interpret debug data.

| Action | Current control |
| --- | --- |
| Select a tile | Left click or tap. |
| Pan | WASD/arrow keys, middle-button drag, or one-finger touch drag. Shift increases keyboard pan speed. |
| Orbit | Q/E, right-button drag, or two-finger twist. |
| Zoom | Mouse wheel or two-finger pinch. |
| Choose a tool | Toolbar or number keys 1–7. Choose the site, then use Apply for construction or a claim. |
| Return to the base | HOME in the toolbar or number key 7 focuses and selects the existing depot. It does not build another depot. |
| Pause | Pause/Resume button or Space. |
| Change simulation speed | 1x/2x button. |
| Save / load | Buttons or F5/F9. |
| Inspect | INSPECT or Escape. |

New requires a second click within four seconds and archives the island being left. Previous reloads that one archived island; it is not a two-way switch and does not archive the island being replaced. There are no named save slots or multi-island history in this version. Removal also requires confirmation, with Core enforcing route and depot protection.

The starter configures landscape orientation for iOS. Touch interaction remains a validation target: a phone must make it easy to distinguish selecting a tile from moving the camera, avoid selecting terrain behind the HUD, and retain clear controls when the camera is close to a quarry. Desktop input alone does not prove those interactions work.

Use prototype geometry to validate world readability and placement before investing in a full asset set. Keep selection, ownership, roads, deposits, and building silhouettes visually distinct.
