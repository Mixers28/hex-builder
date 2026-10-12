# Design decisions

This log records settled direction separately from proposed mechanics. Implementation and verification status remain in [DESIGN.md](DESIGN.md), the relevant slice record and [VERIFICATION.md](VERIFICATION.md).

## 2026-10-10 Planning baseline and long term direction

This section preserves the planning baseline; its implementation statuses are superseded by the build entry below. Source: the user's answers during planning with the game-slice skill. Related slice: [First frontier](slices/001-first-frontier.md).

| Decision | Reason and scope | Status |
| --- | --- | --- |
| Aim for a replayable settlement developing incrementally across weeks. | The user enjoyed the prototype but described it as shallow, fast and largely complete in about fifteen minutes. The long-term target concerns meaningful growth over repeated sessions; offline progress has not been selected. | Agreed direction; not established by the current prototype. |
| Allow players to assign workers to production buildings. | The user wants deeper work management. The exact crew model and output tuning remain proposed. | Agreed direction; current production staffing is automatic. |
| Place migrants into housing automatically over time. | Houses provide capacity and can remain partly occupied. Constructing housing must not instantly fill it or require manual resident assignment. | Agreed rule; gradual immigration already exists, with its twenty-second pacing subject to review. |
| Increase space on each hex relative to buildings and citizens. | The user clarified that individual hexes feel cramped and should accommodate better models and visible village development. More map cells alone would not satisfy this request. | Agreed direction; plot dimensions remain unchanged in code. |
| Conceal the wider world with fog and reveal frontier opportunities through exploration infrastructure. | The village should begin with a limited view and expand its reach. A watchtower is the current proposed structure; generation and reveal details remain open. | Agreed direction; fog and exploration structures are not implemented. |
| Connect expansion to resource unlocks and research into gathering tools. | New discoveries should create new production capabilities and further growth. The first resource and research prerequisites still need selection. | Agreed direction; existing research only unlocks farming, reforestation and deep mining. |
| Improve world presentation and clean up the UI while keeping guidance light. | The user found the current game easy to follow and prioritized appearance, management, size and progression. | Agreed direction; the existing HUD and artwork remain the baseline. |

## Choices open during planning (now resolved)

- Whether the settlement advances only during active play or receives bounded offline progress.
- Whether exploration generates new seeded regions or reveals a larger fixed map.
- Which resource/tool chain and resulting expansion benefit the first frontier slice should demonstrate.

These choices were asked on 2026-10-10, with active play, generated regions and stone/masonry recommended. The later build request accepted those defaults. Final long-term content volume and progression pacing remain open; the first implementation has a bounded world.


## 2026-10-10 Godot build and first frontier

Source: the user's “ok, using godot, lets build it out” following the recommended choices. This explicitly authorizes implementation and changes the engine. “OK” is taken as acceptance of the offered defaults; this is not acceptance inferred from silence. Related slice: [First Frontier](slices/001-first-frontier.md).

| Decision | Reason and scope | Status |
| --- | --- | --- |
| Make standard Godot 4.7.2 / GDScript the active game under `godot/`. | The user explicitly selected Godot. Use the installed standard editor, with no .NET dependency. Preserve the Unity project as the prior baseline. | Implemented; engine import, native rules and rendered runtime checked locally. |
| Advance only during active play across saved sessions. | Accepted recommended default; keeps the first expansion cycle focused on worker and resource decisions. Offline production needs separate scope. | Implemented; no wall-clock catch-up. |
| Generate seeded land on permanent discovery, capped at radius 16 / 817 coordinates. | Accepted generated-region approach. The cap is an implementation limit for this slice, not a promise of endless scaling. Revealed and claimed land stay distinct. | Implemented; two seeds, tower connection, repeated reveal and full-boundary serialization checked. |
| Complete stone → Stoneworking → Quarrying tools → staffed quarry → masonry → reinforced tower. | Accepted recommended single-resource chain, including separate discovery, understanding and extraction capability. No circular stone prerequisite for quarry tools. | Implemented; resource-funded command path and rendered checkpoints checked. |
| Use manual crews of two or three; additional workers add 55% of base work speed. | A bounded first crew model gives allocation a visible effect while keeping delivery distance relevant. This is provisional tuning, not a creative expansion into individual careers or families. | Implemented; limits, throughput, reservations and crew changes checked. |
| Admit one supported migrant per 90 simulated seconds into automatic housing. | Implements the user's gradual occupancy rule and makes vacancies distinct from available labour. Existing residents are not removed for shortages. | Implemented; timing, housing, food and save continuity checked. |
| Use material investment plus one timed research project at a time. | Makes exploration and tool unlocks part of the production economy. Retain the supporting farm/nursery/deep-mine capabilities. Tools remain a capability, not inventory/durability. | Implemented; exact initial costs and times in DESIGN.md. Human balance remains open. |
| Enlarge usable plot area and use original procedural rustic models with a restrained HUD. | The user's visual and UI priorities. No external models, textures, sound or new asset dependency is introduced. | Implemented; actual rendered views and selected pointer/keyboard interactions checked at desktop sizes. |
| Use validated Godot schema-3 JSON with explicit Unity schema-1/2 import. | An intentional persistence change accompanies the engine change. Preserve stock, towns, research, reserves, partial jobs and cargo; retain old known land and never write the imported source. | Implemented; synthetic formats and four existing Unity save artifacts checked. |
| Treat weeks-long progression as the continuing design target. | One working new resource cycle is the agreed playable increment. Timer increases and repeated tower placement do not alone establish long-term engagement. | Unproven; the scripted first payoff took 15.6 simulated minutes. Further content must follow playtest evidence. |

No additional approval step is required for this already-requested build. Combat, multiplayer, arbitrary tunnels, tool durability, extra needs and further resource tiers remain outside this slice. Physical-device validation and an actual weeks-long retention/replayability test remain outstanding.

## 2026-10-10 Equipment, defense, animals and seafaring vision

Source: the user's description of the ideal game after delivery of First Frontier: research weapons and more efficient tools, raise armies against roving barbarians, compete with other villages for resources, discover that the settlement is on an island, build and improve boats to expand, research better refining for better equipment, and introduce animals for meat and leather.

| Decision | Reason and scope | Status |
| --- | --- | --- |
| Develop researched tools and equipment beyond the current quarry capability unlock. | Better equipment should improve work efficiency and enable gathering better resources. | Agreed long-term direction; actual manufactured equipment, assignment and efficiency tiers are not implemented. |
| Progress through better refinement methods. | Improved processing supports better equipment and further resource access. Exact materials, recipes and tiers remain open. | Agreed long-term direction; the current foundry has one ore-to-metal recipe. |
| Research weapons and raise armies for defense against roving barbarians. | Gives production and equipment a defensive purpose. Army control, recruitment, casualties and encounter rules remain open. | Agreed long-term direction; weapons, armies and combat are not implemented. |
| Include villages that compete for resources. | Adds external pressure to resource acquisition and territorial development. Computer-controlled rivals are the planning assumption for the existing single-player game; multiplayer was not requested. | Agreed long-term direction; no rival settlement simulation is implemented. |
| Reveal an island through exploration, then expand by building and improving boats. | Seafaring opens further land and resources that require better equipment. Boat roles and transport mechanics remain open. | Agreed long-term direction; current bounded terrain is not an island/archipelago system and has no vessels. |
| Introduce animals yielding meat, leather and related opportunities. | Extends food and material production. Species, hunting or husbandry, replenishment and processing remain open. | Agreed long-term direction; animals, meat and leather are not implemented. |
| Keep progression connected to the settlement economy and repeated meaningful choices. | Carries forward the user's weeks-long, incremental and replayable growth goal. Longer timers alone do not establish that experience. | Design objective; sustained engagement remains unverified. |

Updated [DESIGN.md](DESIGN.md#long-term-progression-vision) records the direction. The [roadmap](ROADMAP.md) recommends tools/workshop → animals/materials → armed defense → a rival village → first sea crossing → further refining/equipment/boat tiers. That order and the concrete workshop/encounter/transport mechanics are proposals, not user-approved specifications.

This expands the long-term scope beyond the exclusions of Slice 001; those exclusions still accurately describe its delivered build. No game code or build artifact changed for this design update. Active-play-only progress, gradual automatic housing occupancy, manual production staffing and the Godot engine remain the current decisions. Tool durability, naval combat and multiplayer have not been selected.

## 2026-10-10 Retina fullscreen UI correction

Source: the user reports that buttons become too small when the game enters fullscreen on the Mac laptop and asks for a test before the next build. Prioritize this usability correction before the proposed equipment slice.

Reproduced on the Mac's 3024×1964, 2× Retina display (3024×1898 game area in native fullscreen): the unscaled HUD retained its fixed pixel dimensions. Enable `canvas_items` scaling with `expand` aspect and the existing 1440×900 reference layout. Buttons and text now grow with the window while the 3D framebuffer stays at native resolution. This is a presentation correction, with no economy, progression or save-schema change. Automated display checks cover fullscreen, multiple window sizes and pointer coordinates; results are recorded in [VERIFICATION.md](VERIFICATION.md).

## 2026-10-10 Windows friends playtest

Source: the user asks to showcase the small demo to friends and make a Windows executable. Package the existing First Frontier content as a portable **Windows x86_64 release**, with EXE/PCK, play instructions, engine notices and checksums in one ZIP. This is distribution of the current slice, not authorization to add a new progression system or publish publicly.

Use official Godot 4.7.2 templates and retain the Compatibility renderer and fullscreen UI correction. Keep templates and builds in ignored repository folders, with a repeatable export preset and packaging command. The preview is unsigned; no signing identity or installer is introduced. The standard Intel/AMD 64-bit Windows build is the chosen default; ARM-native and 32-bit variants have not been requested.

Validate the export and its actual packed resources on the Mac, and explicitly leave native Windows launch/performance as a PC playtest item. The internal QA harness now writes output outside read-only exported packs; this does not change normal gameplay or save behavior. The [Windows guide](WINDOWS_BUILD.md) and [verification record](VERIFICATION.md) distinguish the built artifact from target-platform runtime evidence.

## 2026-10-10 Save & Exit

Source: the user asks to attach Save & Exit to the current version for players. Add the action to the settlement menu [M] and refresh the Windows playtest ZIP. Save through the existing validated store before quitting; display an error and keep the game open if the write fails. Normal window closure also checks save success when autosaving is enabled. Retain the existing startup protection for unreadable saves, QA isolation, save schema and active-play-only progression. Verification and distribution results are recorded in [VERIFICATION.md](VERIFICATION.md).

## 2026-10-10 Public repository and Windows download

Source: the user requested committing/pushing the work, explicitly chose public `Mixers28/hex-builder`, then authorized publishing the Windows ZIP as a GitHub Release. The [public prerelease v0.1.0-playtest.1](https://github.com/Mixers28/hex-builder/releases/tag/v0.1.0-playtest.1) publishes the tested Save & Exit build from commit `f42965ab405ed74458911b5ebd6328ef2718544f`, with a ZIP checksum and play instructions. Keep generated binaries as release assets and source/export tooling in Git. Prerelease notes retain the outstanding native Windows test; publishing does not establish target-platform compatibility or add game features.

## 2026-10-11 Metal recovery and a more gradual opening

Source: the user's playthrough found a metal shortage that prevents building a foundry. They requested low-quality smelters for manual production, clay and bricks before iron, and more layered/slower progression, informed by Manor Lords and other strategy games.

| Decision | Scope | Status |
| --- | --- | --- |
| Introduce a primitive smelting recovery path. | Reaching first metal must not require refined metal, including resource access and research prerequisites. | User direction accepted; current failure reproduced; implementation pending. |
| Introduce clay and bricks before iron-dependent industry. | Give the opening useful processing stages and competing labour/material investments. | User direction accepted; exact recipes and unlocks proposed in Slice 002. |
| Prioritize this opening before the previously recommended tool workshop. | An equipment economy needs a recoverable material supply. | Planning recommendation based on the playtest. |
| Use timber as the first fuel and retain one refined-metal resource. | Avoid adding charcoal, coal or metal-quality inventories before the small loop is tested. | Proposed implementation scope. |
| Start a timed primitive batch manually with an assigned worker. | Preserve a visible hands-on opening and an automation payoff later. | Recommended answer; user choice still pending versus automatic repetition. |
| Target 45–60 minutes of active play to first useful iron production. | A testable early milestone within the longer-game ambition, not a claim of weeks of content. | Recommended playtest target; user choice still pending versus 2–3 hours. |

The [next slice proposal](slices/002-clay-and-first-metal.md) and [design](DESIGN.md#next-opening-clay-and-first-metal) distinguish agreed direction from unresolved mechanics. Current game rules, saves and published binaries remain unchanged. Production timings require human playtests as well as a command-driven feasibility check.

## 2026-10-11 Windows publisher trust

Source: the user reports an uncertified-software warning, targets UK/EU players, and confirms that the publisher is an individual developer. Target markets do not establish the publisher's legal residence. The exact warning text is still unconfirmed; do not conflate SmartScreen reputation, unknown publisher and a malware detection.

The current EXE is unsigned. Investigate a public code-signing certificate that accepts individual developers for direct GitHub distribution, or Store-certified MSIX distribution. Microsoft Artifact Signing currently limits individual enrollment to US/Canada residents, so it is not the applicable route if the publisher is based in the UK/EU. Certum Standard explicitly supports individuals; it is a candidate, not a purchased or configured service. Signing verifies the publisher but does not guarantee immediate SmartScreen reputation. Sources and release steps are in [Windows builds](WINDOWS_BUILD.md#publisher-trust-and-signing).

No certificate purchase, identity submission, Store account creation or signed release has been performed. Choosing a provider/distribution route and completing the publisher's identity validation precede signing integration.


## 2026-10-11 Return to Unity and implement Clay and First Metal

Source: after discussing the original Unity project and subsequent Godot request, the user said “proceed as recommended.” This accepts the recommendation to resume active development in Unity 6.3 LTS, port the current demo and saves, complete Windows updating, and build Slice 002.

- Unity 6000.3.26f1 is active again; preserve the Godot release as reference. Bring forward manual crews, gradual immigration, larger plots, permanent fog discovery, the stone/tower chain, readable fullscreen controls, save/load and Save & Exit.
- Accept a manually started, timed primitive batch with one worker. Timber is its fuel; one refined-metal counter remains. New villages start with zero metal; claims no longer cost metal.
- Add clay and bricks, research gates, a brick kiln and primitive bloomery before the more efficient foundry. Costs and timings are in DESIGN.md. Make woodland renewal accessible before metal so fuel is a meaningful, renewable investment.
- Accept 45–60 minutes as the first-useful-iron human playtest target. Automated feasibility is separate: first metal 18.2 simulated minutes, operating foundry 31.4. Do not describe that target or weeks of content as measured.
- Keep existing saves through schema 4. Grandfather old industry prerequisites; complete already reserved foundry jobs without retroactive fuel charges. Add one finite clay opportunity without destroying existing development. Retain source files and backups.
- Use a separate, self-contained Windows launcher with Velopack, the public GitHub Releases feed and explicit save-before-restart installation. Existing Godot ZIP users need one initial Unity installation. Source pushes run checks; only published, packaged releases are offered to players.
- Signing remains separate. No identity, certificate or Store enrollment has been acquired, and the updater cannot remove Windows trust warnings.

No armies, boats, animals, multiplayer or offline production were added to this slice. Verify the complete production loop and gather a human pacing/UI playtest before expanding that scope.
