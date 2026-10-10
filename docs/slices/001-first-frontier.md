# Slice 001 · First Frontier

Date: 2026-10-10. Status: implemented in Godot; local technical checks passed. Human balance/replayability and physical-device validation remain open. The original plan was approved with “ok, using godot, lets build it out”; the user explicitly changed the engine and requested the build.

## Experience and assumption

Develop a supported village, assign its people to production, discover a stone frontier, research the means to use it, deliver stone, and invest it in a watchtower that opens further land. The assumption is that labour allocation plus resource-led discovery gives the player a reason to keep developing the same settlement.

The user found the earlier Unity prototype enjoyable and easy to follow but shallow and mostly exhausted after fifteen minutes. Their priorities are deeper work management, larger individual plots, better world presentation, cleaner UI, gradual housing occupancy, concealed map growth, and linked resource/tool research. The longer-term goal is incremental, replayable settlement growth across weeks. One new resource chain cannot yet establish that goal.

Authoritative rules: [DESIGN.md](../DESIGN.md). Agreement and engine/persistence changes: [DECISIONS.md](../DECISIONS.md). Implementation: [ARCHITECTURE.md](../ARCHITECTURE.md). Evidence: [VERIFICATION.md](../VERIFICATION.md).

## Accepted scope and implemented result

- Standard **Godot 4.7.2**, GDScript, native `godot/project.godot`. The Unity baseline is retained; it does not run the active game.
- Active play only, fixed-step simulation, pause and 1×/2×. Saving persists the settlement between sessions; no offline grant.
- Manual production crews, gradual automatic housing occupancy, food and recovery, retained partial jobs and reserved inputs.
- Larger hex plots relative to buildings; procedural rustic cottages, yards, farms, nurseries, industry, quarries, towers, people and cargo. No external art or audio dependency.
- Seeded regions generated as connected towers reveal them. A 37-cell starting view; permanent discovery; unclaimed frontier resources; radius-16 / 817-coordinate limit.
- One new resource: stone. Surveying → discover stone → Stoneworking → Quarrying tools → staffed quarry → delivered stone → Frontier masonry → reinforced tower → further discovery.
- Supporting farm, nursery and deep-iron research, explicitly allocated labour and delivery-dependent output.
- Resource/population summary, construction menu, workforce overview, research panel, contextual inspector and restrained guidance.
- Native saves, validated fallback, one previous-settlement archive and explicit Unity schema-1/2 import preserving the original source.

Exact costs, crew capacities, research times and immigration pacing are provisional implementation tuning documented in DESIGN.md. Additional workers accelerate work but not travel; the producer still waits for its own delivery.

## Start-to-finish path and payoff

The [root README](../../README.md#play-the-slice) gives coordinates for a reproducible starting village. Build housing and staffed food, timber, iron and refining; grow to six citizens, fund surveying and connect a tower at `(0,2)`. Discover stone, fund its understanding and tools, extend the road through `(0,3)` and `(0,4)`, and staff the quarry at `(0,5)`. Use delivered stone for masonry and tower reinforcement. There is no victory screen: the revealed region is the first milestone, and more outposts can follow.

Keep the cost of another resource claim in reserve. Manual gathering is an existing recovery action, not an infinite replacement for depleted owned resources. The automated scenario uses it where needed and spends only through normal validated commands. Seed 417 reached reinforcement in **15.6 simulated minutes / 115 commands**. This is an automated feasibility path with no human decision time, not a measured player session or evidence of the weeks-long goal.

## Preservation and exclusions

Preserved invariants: finite deposits; one owner for stored, reserved and travelling goods; no credit before arrival; one cart per producer; retained road on exhausted-site cleanup; occupied-home/hall/cargo-route removal restrictions; manual food and basic gathering; save continuity; staged raised-column excavation.

No combat, multiplayer, arbitrary tunnels, underground navigation, multiple depots, individual villager careers/families, extra household needs, tool crafting/durability, offline simulation, audio or additional resource tiers. Researching tools grants a capability. Neither a larger tested save boundary nor different seeds proves endless-world performance or replayability.

## Acceptance and evidence

| Criterion | Result | Evidence or limitation |
| --- | --- | --- |
| Houses start empty and fill gradually; housing remains separate from employment. | Passed technically | Native simulation checks plus a pointer/keyboard HUD build of an empty cottage. One arrival per 90 eligible seconds, no silent crew changes. |
| Crew allocation affects production, respects population/capacity, and preserves work/input/cargo. | Passed technically | Crew speed and limits, reservation/refund, delivery accounting and save checks; rendered workforce click changes quarry staffing. |
| Plots provide room around models and terrain, roads, picking and routes align. | Passed locally | Rendered desktop/compact views, actual collider selection, original procedural models and cargo checkpoints. User assessment of appearance remains pending. |
| Fog conceals unknown land; connected towers reveal without claiming or granting resources. | Passed technically | Starting render includes only known tiles; disconnected/connected tower tests, repeated upgrade/reveal checks and permanent discovery through removal/reload. |
| Discovery, resource/tool research, extraction, delivery and resulting expansion are completable without direct resource awards. | Passed technically | Shared scenario runs the actual simulation via validated commands on seeds 417 and 418. Runtime restores and renders real earned-state checkpoints. Selected UI actions are exercised; a full unaided human UI playthrough remains pending. |
| Native and older saves preserve the settlement across the engine/schema change. | Passed locally | Native JSON/backup checks, Unity schema fixtures, four existing Unity save artifacts, unchanged source hashes and continued simulation. The native file-picker import/New/Previous flow still merits a human pass. |
| Player can recognize and address a labour bottleneck with little guidance. | Not run with a player | UI interaction checks establish functionality, not comprehension or enjoyment. |
| Different seeds create useful strategic choices and sustain repeated sessions. | Not established | Two seeds complete the cycle and have differing resources. No comparative human playtest or weeks-long engagement evidence. |
| Physical touch devices and exported builds work correctly. | Native targets not run | Windows x86_64 release produced; its actual PCK passes 86 rule/save and 34 rendered checks with the Mac engine. Windows EXE and physical touch-device behavior remain unverified. |

Local logs and screenshots are linked from VERIFICATION.md. Core tests include the full supported save boundary; that does not establish comfortable performance with every tile developed. Legacy .NET/Unity suites were not rerun for this native port because their code was unchanged.

## Handoff and next playtest

Open `godot/project.godot` and press F5; the updated workspace is the primary delivery. The [complete Godot ZIP](../../Artifacts/alpha-hex-first-frontier-godot.zip) excludes `.godot`, `qa-output` and user saves. The original archive passed clean-extraction import, 86 rule/save checks and 34 rendered assertions. The later Retina fullscreen correction passed 124 display checks plus those 86 simulation and 34 runtime checks in the workspace; the refreshed ZIP was checked byte-for-byte against that source. See [verification](../VERIFICATION.md) for the separate records. Use the README's play path and controls; restart a running session to pick up the display correction.

First observe where the player assigns the fifth through tenth workers, whether the quarry road changes those choices, and whether reinforcement creates a reason to keep playing. Record waiting, repetitive hand gathering, reserve mistakes and the point where another expansion stops feeling useful. Choose the next resource branch or balance experiment from that evidence. No next slice is authorized automatically by this record.

For the authorized friends playtest, share the [Windows x64 ZIP](https://github.com/Mixers28/hex-builder/releases/download/v0.1.0-playtest.1/alpha-hex-first-frontier-windows-x64.zip). It includes the executable, resource pack and play guide. Follow [the Windows build notes](../WINDOWS_BUILD.md) for the first PC launch/save check; native Windows runtime verification remains pending.

The source and Windows ZIPs now include the requested **Menu [M] → Save & Exit** action. Saving must succeed before exit; errors keep the game open. The updated pack passed 86 simulation/save and 39 rendered assertions on Mac, and an isolated lifecycle check verified write failure, retry, exit and startup restore. This is a usability update to the existing slice, with no progression or save-schema change.
