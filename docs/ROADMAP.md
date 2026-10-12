# Roadmap

The target is a replayable settlement that develops incrementally across weeks through gathering, refining, equipment, defense, competing villages and eventual seafaring. Animals add meat and leather to that economy. The [design](DESIGN.md#long-term-progression-vision) and [decisions](DECISIONS.md) record the user's direction; First Frontier does not yet provide these later systems or that duration of content.

The sequence below is a recommendation, not an approved specification for all future slices. Agree the material mechanics when selecting each slice, then build one complete playable payoff. Technical checks, human playtests and save continuity remain part of every delivery.

## 1. Carry First Frontier into Unity

Implemented in the active Unity project: manual crews, gradual occupancy, larger plots, permanent discovery, research, stone extraction and watchtower reinforcement. The released Godot demo remains a compatibility reference. Verify save migration and the actual player builds; see [verification](VERIFICATION.md).

## 2. Playtest Clay and First Metal

Implemented: metal-free claims/start, clay and bricks, a brick kiln, manual bloomery batches and more efficient automatic refining. The user accepted a 45–60-minute human first-iron target. A scripted route reaches first metal at 18.2 minutes and a working foundry at 31.4; human duration and enjoyment are unmeasured. Test useful decisions, idle waits, resource recovery and fullscreen readability before choosing the next chain.

The Windows update feature uses versioned GitHub Releases. It does not replace signing or human Windows testing. [Slice 002](slices/002-clay-and-first-metal.md) records this scope.

## 3. Make and use better tools

Proposed scope: one researched tool tier, one staffed workshop using existing timber and refined metal, and an explicit way to put those tools into use at a production building. Show the cost and resulting efficiency in the inspector. Keep worker availability, finite deposits and delivery distance relevant. This is the first test of equipment as a manufactured good; individual villager inventories and durability are not required to test it.

Settle equipment quantities, workplace assignment and the exact benefit during slice planning. Measure the investment through delivered goods, including the workers diverted into making tools. A faster version of the existing opening is not evidence of more content: this slice establishes the production foundation for later extraction, defense and travel requirements.

**Gate:** the player can decide which workplace deserves the first equipment investment, explain its benefit, and see that choice survive a save/load without duplicated tools or goods.

## 4. Animals and useful materials

Introduce one animal source and a small acquisition/processing chain producing meat and leather. Decide whether the first implementation uses hunting or husbandry and give leather one useful equipment application before extending the animal roster. Meat should have a clear relationship to the existing food economy; its accounting and food value need explicit design.

**Gate:** animals change a food or equipment decision, and the player can follow their products from source to consumption. Species, replenishment and processing recipes remain proposed until scoped.

## 5. First armed defense

Connect one weapon research and production chain to recruitment and equipment of a small defending force. Introduce one roving barbarian threat and a comprehensible defense outcome. Decide army control, warnings, recruitment's effect on the civilian workforce, casualties and recovery before implementation. Prefer a bounded first encounter that tests these choices before adding multiple troop types or repeated attack schedules.

**Gate:** preparation has a visible economic cost, equipment affects the outcome, and the player understands how to protect a useful part of the settlement and recover from setbacks.

## 6. A competing village

Add one persistent computer-controlled settlement competing for finite frontier resources. Settle how it expands, claims resources and interacts with player territory. Give it understandable behavior and actual resource consequences. Diplomacy, trading and additional opponents are separate design choices.

**Gate:** the rival changes an expansion or defense decision and remains consistent across saved sessions. Competition must remain playable without exhausting every recovery route.

## 7. Discover the island and make the first crossing

Let exploration reveal a coherent coastline and the island's limits. Connect boat research and construction to reaching one further island with a resource opportunity the player can use. Choose the first boat's role, transport rules and the equipment required to gather that resource. Deliver a complete return path from overseas discovery into the settlement economy.

This requires a deliberate world-generation and persistence extension: the current 817-coordinate land region is not already an archipelago. Preserve existing discovered land and settlements when introducing the new world structure.

**Gate:** the player builds a boat, crosses to new land, obtains a useful resource through the intended logistics, and saves/resumes with vessels, cargo and discoveries intact.

## 8. Further refining, equipment and boat tiers

Build on those complete loops with better refinement methods, equipment and boats. Add one linked resource → processing → equipment → new opportunity chain at a time. Each tier should change capability, production planning, defense or reach. Different regional resources and competing investments are candidate ways to make new settlements replay differently; they still need playtesting.

**Gate:** a return session brings a meaningful new decision or visible development. Test whether choices remain useful across seeds and repeated sessions before claiming weeks of progression.

## Ongoing: labour, logistics, presentation and scale

Use playtest evidence to tune migration, crew returns, research investment, finite deposits, manual recovery and travel costs. Keep housing automatic and gradual, production staffing explicit, models legible on the larger plots, and the UI clear as systems grow. Further transport depth, inbound ore, local stores and multiple depots need specific accounting and scope decisions.

The first implementation is bounded at radius 16 / 817 hex coordinates. Measure simulation, generation, rendering, save size and memory with developed towns before raising the cap or claiming endless worlds. Choose a physical target and test the actual Godot export, touch UI and lifecycle saves. Existing Unity export instructions do not apply to the active game.

**Gate:** labour and distance create understandable trade-offs, settlements remain recoverable, and the complete loop stays comfortable and reliable at the chosen scale and on the selected hardware.

## Not selected or still unresolved

Weapons, better tools and refining, armies, barbarians, rival villages, islands, boats and animals are now agreed long-term direction. They remain outside the delivered First Frontier build. Equipment wear, individual inventories, detailed combat control, hunting versus husbandry, ship roles and exact progression tiers remain open mechanics.

Offline progress, extra household needs, seasons, taxation, trading, diplomacy, naval combat, arbitrary tunnels and multiplayer have not been selected. Preserve existing save continuity and manual recovery while refining the game. No next slice starts automatically from this roadmap.
