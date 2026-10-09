# Roadmap

This roadmap uses completion gates rather than dates. The repository is a prototype foundation; features listed as future work are not implied to be implemented.

## 1. Validate the current playable foundation

Open the project with its pinned Unity Editor version, create the playable scene with `ProjectSetup`, and complete the manual checks in `QA.md`. Resolve import errors, runtime exceptions, misleading resource feedback, and failures to finish the extraction/refining/expansion loop.

**Gate:** a new player can gather resources, connect a mine and smelter, see deliveries arrive, claim additional land, and restore that progress from a local save. Capture an actual Play Mode result rather than inferring it from the console tests.

## 2. Make the economy understandable

Tune starter resources, building costs, extraction rates, conversion ratios, deposit sizes, and route travel time together. Explain why an action fails and why a producer is waiting. Show available stock separately from shipments in transit.

Observe whether players make interesting placement and expansion decisions. Add goals or guidance around existing actions before adding a broad technology tree.

**Gate:** a playtester can explain their next useful construction or expansion choice and identify the limiting resource or production stage.

## 3. Decide how far to deepen logistics

Potential extensions include explicit inbound ore deliveries, finite carrying capacity, local building inventories, and visible transport assignments. Select the smallest addition that solves an observed weakness in the first economy.

Introducing two-way logistics requires defining reservation, dispatch, arrival, blocked routes, full destinations, cancellation, and save restoration. Do not charge an input at both reservation and consumption, or award output at both dispatch and arrival.

**Gate:** a chosen logistics extension produces a visible tradeoff, handles interruption without losing or duplicating goods, and survives save/load.

## 4. Validate the iOS experience

Perform an actual iOS export and run on a chosen physical device. Measure touch selection, camera gestures, HUD layout, safe areas, loading, frame time, memory, and lifecycle behavior. Tune geometry and update frequency from those measurements.

Validate the configured landscape orientation and choose a minimum device target from that experience. A working Unity desktop session does not establish acceptable mobile behavior.

**Gate:** the complete loop is comfortable on the target device and saving remains reliable across app suspension and restart.

## 5. Expand content after the loop works

Candidates include additional deposits, a second refining recipe, clearer quarry stages, more island variations, construction presentation, sound, and a small progression system. Introduce one economic relationship at a time so its effect can be evaluated.

If longer-lived saves are needed, establish tested schema migrations before distributing updates that change persisted state.

**Gate:** new content creates a distinct choice while keeping the original progression and older supported saves usable.

## Decisions deliberately left open

| Decision | Consequence |
| --- | --- |
| True volumetric excavation | Requires a terrain representation and underground selection design beyond staged hex quarries. |
| Larger or endless worlds | Requires generation, simulation, rendering, and save scaling beyond the bounded prototype. |
| More depots or local warehouses | Requires explicit stock ownership and routing rules instead of assuming one shared destination. |
| Multiplayer or cooperative building | Requires an authority model, synchronization, identity, persistence, and conflict handling. |
| PvP or combat | Requires a separate gameplay decision; it is not necessary to complete the current builder. |
| Offline production | Requires explicit time, cap, clock manipulation, and restore rules; it is not present now. |

These decisions are not commitments. Keep the current construction and extraction loop playable while considering them.
