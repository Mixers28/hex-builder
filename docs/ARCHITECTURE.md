# Architecture

## Purpose and boundaries

This is a local Unity 6.3 industrial builder prototype. The exact Editor patch is pinned in `ProjectSettings/ProjectVersion.txt`; use that value rather than selecting an arbitrary Unity release. The playable world consists of raised hex columns with staged quarry depth. It is not a general voxel terrain engine.

The main boundary is between game rules and their presentation. Core code can run without Unity. Unity renders the current state, collects input, advances the simulation, and handles local persistence.

| Area | Main types | Responsibility |
| --- | --- | --- |
| Core coordinates | `HexCoord` | Hex positions, neighbours, and distance calculations. |
| Core state | `WorldState`, `TileState`, `Shipment` | Serializable gameplay data: terrain, claims, resources, buildings, stock, and goods in transit. |
| Core rules | `Simulation` | World generation, validated player commands, resource conversion, connectivity, and shipment progression. |
| Runtime coordination | `GameBootstrap` | Create or load a session, connect runtime components, and advance Core. |
| Runtime presentation | `HexWorldView`, `OrbitCamera` | Build the procedural scene from state and provide world navigation. |
| Runtime interface | `GameHud` | Show state and feedback; translate player intent into validated commands. |
| Editor tooling | `ProjectSetup` | Set up the playable project and scene in the Unity Editor. |

## State and commands

The world is deterministic for a given seed and generation implementation. Its initial radius is six hexes, with the radius-one area around the starting base already claimed. Determinism makes new sessions and rule tests reproducible; it does not make different future generator versions interchangeable.

`WorldState` is the gameplay source of truth. The scene must not become an alternative inventory, building registry, or terrain database. A mesh, label, material, or animated carrier is a view of state.

Player actions follow this sequence:

1. A runtime component identifies the selected tile and requested action.
2. The HUD or bootstrap submits the corresponding `Simulation.Try*` command.
3. Core validates the action, including relevant tile, ownership, resource, and cost conditions.
4. A successful command changes state. A rejected command returns feedback without partially spending stock or placing a building.
5. The runtime refreshes the relevant presentation.

Unity input handlers must not directly subtract stock, grant resources, claim tiles, or place production buildings. New actions belong in Core before being wired into the interface.

## Production and transport

The depot stock is shared. Roads and buildings form the transport network. Outbound production reaches that stock through shipments with physical travel delay; visible movement must reflect the same route and progress used by the simulation. Each producer waits for its own shipment to arrive before starting its next production cycle. A longer route therefore reduces that producer's effective output rate.

The prototype deliberately simplifies refinery input: a connected smelter reserves ore from shared network stock. It does not request a separate inbound delivery. Consequently this is not a complete two-way logistics simulation, even though outbound goods have travel time. Manual gathering credits the depot immediately, including when the selected tile is disconnected; this is a separate bootstrap action, not a shipment.

Keep the distinction between stored goods, reserved inputs, and goods in transit explicit. Dispatch, travel, and arrival must not grant the same resource more than once. Production and transport rules belong in Core; animation must not award goods when an object reaches a visual destination. Demolition does not refund construction costs. It returns reserved, unprocessed ore where relevant and rejects removal of the depot or a tile used by an active shipment.

Core automatically clears exhausted forestry and mines once their own outbound shipment has arrived. Clearing retains an access road, preserving the network topology and other shipments that cross the site. Manual gathering clears an exhausted idle extractor immediately; fixed-step cleanup also handles depleted buildings in older saves. No save schema change or construction refund is involved. The presentation refreshes the changed building from state.

Core advances in fixed 0.125-second steps and preserves the fractional remainder in state. Runtime supplies foreground frame time at the selected 1x or 2x speed, caps the contribution of a long frame, and can pause advancement. Saving a timestamp does not authorize offline production or a wall-clock catch-up calculation. There is no background simulation service.

## Persistence

Runtime persistence uses a local, versioned `JsonUtility` save. Persist plain gameplay data, not `GameObject` references, materials, meshes, camera objects, or transient selection highlights. Recreate visuals from validated state after loading.

Treat a save file as input that can be missing, damaged, edited, or from another version. Validate the format version and gameplay structure before replacing the active session. Do not silently interpret an unsupported version as the current schema. Migration support should be explicit when introduced.

Validation must protect the same invariants as player commands: valid coordinates and tile identities, legal enum values, nonnegative finite quantities and progress, consistent stock and resource fields, and coherent shipment routes. A loader must not leave a partly restored world active after rejecting a save.

The runtime normally autosaves every 30 seconds and attempts to save on application suspension and exit. A valid prior primary save can provide a recovery backup. An unreadable save discovered at startup disables automatic overwrite for that session until an explicit successful save or world replacement.

The separate Previous island archive stores the active island immediately before a confirmed New operation. It survives routine autosaves and is distinct from the ordinary save backup. Previous loads that single archive; it does not swap worlds or create a history of every island. New archives and saves successfully before replacing the active simulation.

## Development and tests

The console test project targets .NET 10 with C# 9 language compatibility. It links the same Core source files used by Unity and requires no NuGet test framework. Do not copy Core into a separate test implementation, or use .NET 10-only APIs in source that must also compile in Unity.

Keep Core free of `UnityEngine`, `UnityEditor`, frame input, scene objects, and Unity serialization callbacks. Unity-specific setup and presentation need separate Editor and device validation. A successful console run verifies only the exercised rules, not the Unity import, rendering, touch controls, or iOS export.

This is a local prototype. It has no multiplayer synchronization, authenticated command processing, server authority, or adversarial client boundary. Separating rules from presentation is useful groundwork, but does not make the game ready for networked play.
