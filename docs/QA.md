# QA and acceptance checks

## Verification status

This document is a checklist, not a test execution report. A check being listed here does not mean it has passed.

The initial starter was prepared without Unity Editor or device validation. Later local Editor and test results are recorded in [VERIFICATION.md](VERIFICATION.md); this checklist does not imply a complete QA pass. iOS export and physical-device validation remain outstanding. Console rule tests are a separate check; their actual execution result should be recorded with the revision tested.

Use the setup and test commands in the root README. Do not infer Unity compilation or device performance from a successful .NET console run.

## Core rule checks

The console test project must link the actual Unity-independent Core source. Use deterministic seeds and simulated time rather than sleeping or depending on frame rate.

| Area | Expected result |
| --- | --- |
| Coordinates | Neighbour and distance operations behave consistently in all six directions. |
| Generation | The same seed reproduces the same starting map; radius six contains 127 unique coordinates. |
| Starting state | The radius-one starting territory is claimed and the base is valid. |
| Commands | Invalid claims, gathers, builds, and conversions are rejected without partial state changes. |
| Costs | Successful commands spend their cost exactly once; resources never become negative. |
| Gathering | Manual actions respect the selected tile and available resources, and grant stock immediately even when disconnected. |
| Extraction | Production consumes deposits consistently and quarry depth remains within supported stages. |
| Connectivity | Production depends on a valid road/building route to the depot under the implemented rules. |
| Transit | Dispatched goods remain in transit until arrival and are credited exactly once; the source producer waits for delivery before restarting. |
| Refining | Ore and refined output follow the recipe; connected stock withdrawal matches the documented input simplification. |
| Frontier | Expansion follows ownership and adjacency rules and cannot claim an invalid coordinate. |
| Demolition | Construction costs are not refunded; reserved, unprocessed ore is returned where relevant. Depot and active shipment route tiles cannot be removed. |
| Time | Progress responds consistently to the supported simulation time steps; invalid time values cannot corrupt state. |

Add regression cases for demonstrated rule defects. Keep broad gameplay experiments and visual assertions out of this plain C# test runner.

## Unity Editor and Play Mode

Run these checks using the Editor patch pinned in the project:

- Import the project and run `ProjectSetup`. Confirm a playable scene opens without missing scripts, assets, or shader errors.
- Enter Play Mode and verify that the generated island, raised columns, forests, deposits, roads, and buildings are readable.
- Select tiles at the centre, at the map edge, and beside taller columns. Verify selection matches the visible hex.
- Move, rotate, and zoom the camera. Verify reasonable limits and that UI interaction does not trigger an unintended world action.
- Use HOME and number key 7. Confirm they focus and select the existing depot without placing a building or spending resources.
- Gather timber and ore. Inspect stock and tile resource feedback before and after each action.
- Place forestry, a mine, and a smelter under valid conditions. Try invalid terrain, insufficient resources, and already occupied tiles.
- Observe an unconnected producer, connect it, and follow a shipment to the depot. Verify displayed stock changes at the appropriate stage.
- Observe refining. Confirm the UI does not imply a physical inbound ore delivery that the simulation does not perform.
- Extract enough ore to change quarry depth. Check the visible stage, selection, road representation, and camera readability.
- Deplete a forest and a deposit. Verify the building remains for its final delivery, then clears while its access road remains. Repeat by hand gathering, including during an outgoing shipment. Check grass/rock terrain, quarry depth, stock, and continued deliveries from a producer routed through the cleared tile.
- Save during an exhausted extractor's final delivery and reload. Verify the delivery arrives once and the building clears. Load an older save with an idle exhausted building and confirm cleanup occurs when simulation resumes.
- Arm manual building removal just before depletion cleanup. After the building clears, verify another confirmation is required to remove the remaining road.
- Demolish an eligible building, then try the depot and a tile carrying an active shipment. Verify allowed refunds and rejection behavior.
- Claim a valid frontier tile, then try an invalid or unaffordable claim. Verify the failure is explained and stock is unchanged.
- Complete an extraction/refining/expansion loop without editing state in the Inspector.
- Leave the session running and inspect the Console for exceptions, excessive repeated warnings, or growing numbers of unused scene objects.

## Save and restore

Record the state before saving so the comparison is concrete: seed, claims, buildings, resources, stock, quarry stages, and any shipments in transit.

1. Save, restart the session, and load. Verify the world and economy restore rather than regenerating an unrelated map.
2. Save with a shipment travelling and a smelter active. Restore and let them finish. Check for duplicate goods, lost reservations, or repeated recipe charges.
3. Test a missing save, malformed JSON, an unsupported version, and invalid world data. Confirm rejection produces useful feedback and does not replace the session with a partly loaded world.
4. Test duplicate or out-of-range coordinates, illegal resource quantities, and invalid shipment data against loader validation.
5. Quit and resume after elapsed real time. Confirm no undocumented offline production is granted.
6. Confirm New twice, then allow ordinary autosaves and choose Previous. Confirm the separate pre-New island archive still restores. Verify Previous is a single archive load, not a two-way swap or a save of the island being left.
7. Simulate an archive or new-save failure. Confirm New leaves the active island intact. Start with an unreadable primary save and check that automatic saving does not immediately overwrite it.

Use copies of saves when testing corruption. Save migrations, cloud sync, and concurrent devices are outside the current feature set.

## iOS export and physical-device checks

These checks require the actual iOS build pipeline and a device; they cannot be completed by the Core runner:

- Export and compile the project through the intended Unity/Xcode workflow. Record the Editor patch, Xcode version, device model, and OS version used.
- Start from a clean install and complete the same playable loop using touch only.
- Confirm tap selection, camera movement, rotation, and zoom do not conflict with one another or with the HUD.
- Check text, controls, safe areas, and selection feedback at the supported orientation and screen sizes.
- Measure frame time, memory, loading, and sustained behavior during a developed session with production and shipments active.
- Background and resume the app, interrupt it during play, and restart it. Confirm the expected persistence behavior and absence of duplicate production.
- Test local saving and loading on the device rather than relying on the Editor's writable directory.

## Recording results

For each verification session, record the revision, environment, checks actually performed, result, and unresolved defects. Distinguish a Core test pass, an Editor pass, an exported build, and a physical-device pass. None substitutes for the others.
