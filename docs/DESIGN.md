# Game design · Alpha Hex

## Intended experience and agreed direction

Develop a small frontier village by supporting gradual immigration, choosing where people work, and turning discoveries into productive capabilities. The user wants a visually richer, replayable settlement that grows incrementally across weeks. Their 2026-10-10 playtest found the earlier Unity loop enjoyable and understandable but shallow and effectively exhausted after about fifteen minutes.

The active **Godot 4.7.2** slice implements one complete new frontier cycle. This is groundwork for that longer game, not evidence that weeks of content or replayability already exist. The engine change and build were explicitly requested after planning. Active-play-only progress, seeded generation on discovery, and the recommended stone/tool/watchtower chain were accepted with the build request. See the [decision log](DECISIONS.md) and [slice record](slices/001-first-frontier.md). The [Unity rules](UNITY_DESIGN.md) are a historical baseline.

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

## Current playable slice: First Frontier

The rules and balance below describe the delivered Godot build. Future equipment, warfare, animal and maritime systems above do not change these implemented rules yet.

### Playable loop

1. Begin with four pioneers at the village hall, seven owned plots, 80 timber, 10 metal and 24 food. A radius-three area of 37 hexes is visible.
2. Build empty cottages and a food producer. Assign its crew. Eligible migrants arrive one at a time and occupy connected housing automatically; each newcomer becomes an available worker.
3. Allocate workers to timber, iron extraction and metal refining. Crew choices affect work speed. Roads, finite deposits and each producer's delivery wait remain meaningful constraints.
4. Research surveying, build a connected watchtower and discover more land. Revealed land is unclaimed and its resources cannot be collected until developed.
5. Discover stone, research Stoneworking and Quarrying tools, claim and connect an outcrop, then staff a quarry. Follow its stone into the shared village stores.
6. Research masonry and spend delivered stone to reinforce a watchtower. The wider survey opens another frontier. Save and continue the same village; there is no population victory condition or end screen.

Farming, tree nurseries and finite deep mining support further settlement growth. Additional resource tiers, differentiated region objectives and long-term research branches are future slices. Repeating the present tower/quarry cycle is possible, but does not by itself establish durable replayability.

## Housing, migration and workers

Housing is filled automatically; production staffing is manual. Four pioneers remain at the hall, and each cottage holds four newcomers. Building a cottage does not award population. One citizen arrives after **90 seconds of eligible time** with a connected vacancy, no missed-meal flag, and food stock at least equal to population plus one. Arrival consumes two food. Progress pauses when these conditions fail.

Meals occur every 30 seconds and consume one food per two citizens, rounded up. Shortages consume available food and pause immigration; they do not kill or evict anyone or halt recovery production. A later supplied meal clears the shortage. Occupied cottages cannot be demolished. Existing residents of a disconnected home remain citizens and can work, although that home cannot receive further arrivals.

A producer has two or three crew places. Work speed is **100%, 155%, 210%** for one, two or three workers; zero workers means no work. These are initial balance choices, not tested long-term targets. No assignments are made silently when new people arrive. A disconnected workplace can retain its assigned crew but explains why it is idle. Total assignments cannot exceed population. Pausing releases the crew; resuming still requires assignment.

Changing crew preserves legitimate partial work and reserved foundry input. Dispatched cargo continues moving independently. Each producer waits for its own cart before restarting, so extra workers cannot eliminate the cost of long roads. No individual identities, families, skill levels or equipment inventories are simulated.

## Construction and production balance

Durations exclude outbound travel. Costs are paid once at successful construction. Each hex holds one primary building.

| Building | Cost | Crew | Output / base work time | Requirement |
| --- | --- | --- | --- | --- |
| Cottage | 10 timber | — | Four housing places | Clear land |
| Forager lodge | 8 timber | 2 | 3 food / 12 s | Grass or woodland |
| Woodcutter | 12 timber | 3 | One tree → 3 timber / 12 s | Standing trees |
| Iron mine | 16 timber, 2 metal | 3 | Up to 2 ore / 15 s | Surface iron |
| Foundry | 20 timber, 4 metal | 3 | 2 reserved ore → 1 metal / 18 s | Clear land |
| Farmstead | 18 timber, 3 metal | 3 | 6 food / 24 s | Farming; clear grass without ore history |
| Tree nursery | 18 timber, 4 metal | 2 | Plant one neighbouring sapling / 16 s | Woodland stewardship; clear grass |
| Deep mine | 26 timber, 8 metal | 3 | Up to 2 deep ore / 18 s | Deep mining; exhausted surface iron |
| Watchtower | 24 timber, 4 metal | — | Reveal radius 4 when connected | Frontier surveying; clear land |
| Stone quarry | 22 timber, 6 metal | 3 | Up to 2 stone / 20 s | Quarrying tools; stone deposit |

Claims cost **6 timber and 1 metal**, need adjacent owned land and must already be revealed. Roads cost **2 timber**. Buildings also connect roads. Keep the cost of another resource claim in reserve before exhausting existing supplies; food recovery does not replace missing timber or metal.

Manual gathering on owned plots supplies three timber per tree or one surface ore per excavation, even without a road. Hand foraging supplies two food from owned grass/woodland with a global eight-second cooldown. Stone and deep iron require their researched extractors. Manual gathering cannot refund construction or create a depleted deposit.

## Research

Research uses both materials and saved work time, with one active project. Materials are committed once when it begins. In First Frontier, projects are permanent capabilities and tools are an unlock; crafted equipment and durability are not implemented. The long-term equipment direction above extends this model in future slices. Discovery precedes stone research, and quarry tools require no stone, avoiding a circular prerequisite.

| Project | Citizens | Cost | Time | Prerequisite and result |
| --- | --- | --- | --- | --- |
| Farming | 6 | 16 timber, 2 metal | 90 s | Unlock farmsteads |
| Frontier surveying | 6 | 16 timber, 3 metal | 90 s | Unlock watchtowers |
| Woodland stewardship | 8 | 18 timber, 4 metal | 120 s | Unlock tree nurseries |
| Stoneworking | 6 | 12 timber, 4 metal | 90 s | Surveying and discovered stone; opens tool research |
| Quarrying tools | 8 | 24 timber, 8 metal | 150 s | Stoneworking; unlock quarries |
| Frontier masonry | 10 | 20 timber, 6 metal, 10 stone | 180 s | Quarrying tools; unlock tower reinforcement |
| Deep mining | 10 | 30 timber, 8 metal | 180 s | Unlock finite deeper iron extraction |

Reinforcing a connected watchtower costs **12 timber, 4 metal and 16 stone** and increases its reveal radius from four to six. It can be done once per tower. Other new towers can reveal further regions. Existing unused Unity research points remain spendable once for the original Farming, Woodland stewardship or Deep mining unlocks; they do not buy frontier research.

## Frontier and visuals

The world generates reproducibly from seed and coordinates as towers reveal land. The supported radius is **16**, up to **817 hex coordinates**, including water. This is a bounded first implementation, not an endless world. Initial known/owned areas are separate. Fog conceals unexplored terrain and deposits; camera bounds follow discoveries rather than disclosing the whole map.

Discovery is permanent through save/load, tower removal and rebuilding. It never awards resources or ownership, regenerates a depleted deposit, or hides previously visible legacy land. Starting forests, iron and the first stone opportunities are guaranteed; other terrain quantities and locations vary with seed. Those differences are implemented, while strategically different openings still require human testing.

Plots are larger relative to structures: hex radius 2.35, cottage body roughly 1.2 units wide, with yards, gardens, timber framing, fences, logs, crop rows, quarry hoists and tower foundations. Original code-generated geometry supplies all active game art. A cream/green HUD and distinct resource/building silhouettes retain the rustic direction. There are no imported textures, external models or audio files in the Godot project.

Visible citizens derive from the census and crew assignments. Their home/work movement is decorative. Carts represent real saved shipments and use their actual route and time; animation cannot create inventory. Surface ore, deep ore and stone excavation each have three height stages. This remains a raised-column world with no arbitrary tunnels or underground navigation.

## Finite resources and transport

Output travels at **1.25 seconds per edge** to the shared hall. A producer waits for its own delivery. A foundry takes input from shared stock and reserves it before working; there is no separate inbound ore cart. Stored, reserved and travelling goods are distinct and survive loading. Imported cargo completes its original journey at the saved duration.

Trees and mineral deposits are consumed at dispatch, with a final partial ore/stone batch if needed. Deep iron starts at twice the original surface deposit and never refills. Exhausted extractors clear after their last delivery, leave an access road and release their workers. Logging beside a nursery stays for regrowth.

A nursery chooses adjacent owned grass/woodland with no mineral history, fewer than twelve trees, no current sapling and no building except a woodcutter. It prefers fewer trees and then an existing logging site. One sapling matures after **90 seconds**, even if the nursery is removed. Construction over a growing sapling is restricted. Removal refunds no construction costs, returns unused foundry ore once, and rejects the hall, occupied housing and active cargo routes.

## Interface, persistence and limits

The resource strip distinguishes stored and travelling goods. Population shows capacity, working and available people. Build [B], Workers [T], Research [R] and the contextual inspector expose the next action, crew controls, migration status, prerequisites, work/delivery progress and waiting reasons. Guidance stays a single quiet line, prioritizing food or expansion-reserve shortages. Destructive removal requires a second click for the same structure.

The interface scales from a 1440×900 reference layout as the window grows, including Retina fullscreen. Different aspect ratios expose additional layout space without stretching text. The 3D world renders at the actual window resolution. The fullscreen fix changes presentation only; production, research, migration and saves retain their existing rules.

The settlement menu [M] includes **Save & Exit**. It saves the current village before closing; a failed save displays the error and leaves the game open for retry. Normal window closure uses the same checked save when autosaving is enabled. Startup still preserves an unreadable save by disabling automatic writes until an explicit save or replacement.

Desktop mouse/keyboard is the acceptance target. Controls and the start-to-finish path are in [README](../README.md). A [Windows x86_64 playtest package](WINDOWS_BUILD.md) is available; its exported resource pack was checked with the Mac engine, while native Windows execution remains unverified. Touch handlers are present, but physical-device usability, safe areas and mobile exports remain unverified. Godot native schema-3 saves, fallback recovery, a previous-settlement archive and explicit Unity schema-1/2 import preserve continuity. Time advances only during active play; there is no offline production or background service.

The current content is one new stone/tool/masonry chain plus the supporting town systems. No combat, multiplayer, extra settlement needs, seasons, arbitrary tunnels, multiple depots, tool crafting/durability or additional resource tiers are included. The first playtest should establish whether reallocating workers and choosing another frontier feels worthwhile, and where the economy becomes repetitive; longer timers alone do not meet the weeks-long goal.
