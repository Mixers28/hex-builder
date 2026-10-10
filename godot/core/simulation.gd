class_name FrontierSimulation
extends RefCounted

const Rules = preload("res://core/rules.gd")
var state: Dictionary = {}
var tiles: Dictionary = {}
var revision = 0
var network_dirty = true
var routes: Dictionary = {}

func start(seed_value: int = 417) -> void:
	state = {"schema":Rules.SCHEMA, "seed":seed_value, "clock":0.0, "remainder":0.0,
		"stock":{"timber":80,"ore":0,"metal":10,"food":24,"stone":0},
		"population":4, "migration":0.0, "meal":0.0, "forage":0.0, "hungry":false,
		"research":[], "research_job":"", "research_progress":0.0, "shipments":[], "expansions":0,"legacy_research_points":0}
	tiles.clear()
	reveal(Vector2i.ZERO, Rules.START_REVEAL)
	for id in tiles:
		tiles[id].claimed = Rules.distance(Rules.coord(id)) <= 1
	tiles["0,0"].building = "depot"
	tiles["0,0"].road = true
	network_dirty = true
	revision += 1

func generate(c: Vector2i) -> Dictionary:
	var value = (c.x * 92837111 ^ c.y * 689287499 ^ int(state.seed) * 283923481) & 0x7fffffff
	value = ((value ^ (value >> 13)) * 1274126177) & 0x7fffffff
	var roll = value % 100
	var d = Rules.distance(c)
	var terrain = "forest" if roll < 34 else "ore" if roll < 49 else "stone" if roll < 62 and d >= 4 else "grass"
	if d > 5 and roll > 96:
		terrain = "water"
	if d <= 1 or c in [Vector2i(0,2), Vector2i(0,3), Vector2i(0,4)]:
		terrain = "grass"
	if c in [Vector2i(-1,0), Vector2i(-2,0)]: terrain = "forest"
	if c in [Vector2i(1,0), Vector2i(2,0)]: terrain = "ore"
	if c in [Vector2i(0,5), Vector2i(5,-2), Vector2i(-5,2)]: terrain = "stone"
	var trees = 10 + value % 9 if terrain == "forest" else 0
	var ore = 48 + value % 45 if terrain == "ore" else 0
	var stone = 80 + value % 61 if terrain == "stone" else 0
	return {"q":c.x,"r":c.y,"terrain":terrain,"height":0 if d <= 1 else value % 3,
		"claimed":false,"road":false,"building":"","trees":trees,"ore":ore,"initial_ore":ore,
		"deep":ore * 2,"initial_deep":ore * 2,"stone":stone,"initial_stone":stone,
		"workers":0,"paused":false,"residents":0,"work":0.0,"reserved":0,
		"sapling":-1.0,"tower_level":0,"surveyed_level":0}

func reveal(center: Vector2i, radius: int) -> int:
	var added = 0
	for q in range(center.x-radius, center.x+radius+1):
		for r in range(center.y-radius, center.y+radius+1):
			var c = Vector2i(q,r)
			if Rules.distance(c,center) > radius or Rules.distance(c) > Rules.WORLD_LIMIT: continue
			var id = Rules.key(c)
			if not tiles.has(id):
				tiles[id] = generate(c)
				added += 1
	if added > 0:
		revision += 1
		network_dirty = true
	return added

func result(ok: bool, message: String) -> Dictionary:
	return {"ok":ok,"message":message}

func can_pay(cost: Dictionary) -> bool:
	for resource in cost:
		if state.stock.get(resource,0) < cost[resource]: return false
	return true

func pay(cost: Dictionary) -> void:
	for resource in cost: state.stock[resource] -= cost[resource]

func owned_resource(resource: String) -> int:
	var total = int(state.stock[resource])
	for cargo in state.shipments:
		if cargo.resource==resource: total += int(cargo.amount)
	if resource=="ore":
		for t in tiles.values(): total += int(t.reserved)
	return total

func can_store(resource: String, amount: int) -> bool:
	return owned_resource(resource)+amount<=Rules.STOCK_LIMIT

func owned(id: String) -> bool:
	return tiles.has(id) and tiles[id].claimed and tiles[id].terrain != "water"

func try_claim(id: String) -> Dictionary:
	if not tiles.has(id): return result(false,"Explore this land with a watchtower first.")
	if tiles[id].claimed: return result(false,"This land already belongs to your village.")
	if tiles[id].terrain == "water": return result(false,"Choose dry land.")
	var adjacent = false
	for other in Rules.neighbours(id):
		if owned(other): adjacent = true
	if not adjacent: return result(false,"Claim land adjoining your village.")
	var cost = {"timber":6,"metal":1}
	if not can_pay(cost): return result(false,"A claim needs " + Rules.cost_text(cost) + ".")
	pay(cost)
	tiles[id].claimed = true
	changed()
	return result(true,"Land claimed. Roads connect it to your village.")

func build_error(id: String, kind: String) -> String:
	if not Rules.BUILDINGS.has(kind): return "Choose a building."
	if not owned(id): return "Claim this land before building."
	var t = tiles[id]
	var rule = Rules.BUILDINGS[kind]
	if t.building != "": return "This plot already has a building."
	if rule.research != "" and not rule.research in state.research:
		return "Research " + Rules.RESEARCH[rule.research].name + " first."
	if t.sapling >= 0 and not kind == "logging": return "Let the planted sapling grow first."
	if kind == "logging":
		if t.trees <= 0: return "A woodcutter needs standing trees."
	elif kind == "mine":
		if t.ore <= 0: return "An iron mine needs surface ore."
	elif kind == "deep_mine":
		if t.ore > 0 or t.deep <= 0: return "Exhaust surface iron before opening a deep mine."
	elif kind == "quarry":
		if t.stone <= 0: return "A quarry needs a stone deposit."
	elif kind == "forager":
		if t.terrain not in ["grass","forest"]: return "A forager needs grass or woodland."
	else:
		if t.trees > 0 or t.ore > 0 or t.stone > 0: return "Choose clear land or gather its surface resources."
		if kind in ["farm","forester"] and (t.terrain != "grass" or t.initial_ore > 0):
			return "Choose clear grassland for this building."
	if not can_pay(rule.cost): return "Need " + Rules.cost_text(rule.cost) + "."
	return ""

func try_build(id: String, kind: String) -> Dictionary:
	var error = build_error(id,kind)
	if error != "": return result(false,error)
	var t = tiles[id]
	pay(Rules.BUILDINGS[kind].cost)
	t.building = kind
	t.work = 0.0
	t.workers = 0
	t.reserved = 0
	t.paused = false
	if kind == "watchtower": t.tower_level = 1
	changed()
	var suffix = " Assign a crew to begin." if Rules.BUILDINGS[kind].crew > 0 else ""
	if path(id).is_empty(): suffix = " Connect it to the village with roads."
	return result(true,Rules.title(kind) + " built." + suffix)

func try_road(id: String) -> Dictionary:
	if not owned(id): return result(false,"Claim this land before laying a road.")
	if tiles[id].road or tiles[id].building != "": return result(false,"This plot already connects neighbouring roads.")
	if not can_pay({"timber":2}): return result(false,"A road needs 2 timber.")
	pay({"timber":2})
	tiles[id].road = true
	changed()
	return result(true,"Road laid.")

func assigned() -> int:
	var count = 0
	for t in tiles.values(): count += int(t.workers)
	return count

func free_workers() -> int:
	return int(state.population) - assigned()

func try_workers(id: String, count: int) -> Dictionary:
	if not owned(id): return result(false,"Choose one of your workplaces.")
	var t = tiles[id]
	var capacity = int(Rules.BUILDINGS.get(t.building,{}).get("crew",0))
	if capacity == 0: return result(false,"This building does not employ production workers.")
	if count < 0 or count > capacity: return result(false,"This workplace has %d crew places." % capacity)
	if count - int(t.workers) > free_workers(): return result(false,"No available citizens. Reassign workers or welcome migrants.")
	t.workers = count
	t.paused = false
	revision += 1
	return result(true,"%s: %d / %d workers." % [Rules.title(t.building),count,capacity])

func try_pause_work(id: String) -> Dictionary:
	if not owned(id) or Rules.BUILDINGS.get(tiles[id].building,{}).get("crew",0) == 0:
		return result(false,"Choose a production building.")
	var t = tiles[id]
	t.paused = not t.paused
	if t.paused: t.workers = 0
	revision += 1
	return result(true,"Crew released. Travelling goods still arrive." if t.paused else "Work resumed. Assign an available crew.")

func try_gather(id: String) -> Dictionary:
	if not owned(id): return result(false,"Gather on your own land.")
	var t = tiles[id]
	if t.trees > 0:
		if not can_store("timber",3): return result(false,"Timber stores are full.")
		t.trees -= 1
		state.stock.timber += 3
		if t.trees == 0: t.terrain = "grass"
	elif t.ore > 0:
		if not can_store("ore",1): return result(false,"Ore stores are full.")
		t.ore -= 1
		state.stock.ore += 1
	else: return result(false,"No surface timber or iron here. Stone needs researched quarry tools.")
	cleanup(id)
	revision += 1
	return result(true,"Gathered into village stores.")

func try_forage(id: String) -> Dictionary:
	if not owned(id) or tiles[id].terrain not in ["grass","forest"]:
		return result(false,"Forage on owned grass or woodland.")
	if state.forage > 0: return result(false,"Forage again in %d s." % ceili(state.forage))
	if not can_store("food",2): return result(false,"Food stores are full.")
	state.stock.food += 2
	state.forage = Rules.FORAGE_SECONDS
	return result(true,"Gathered 2 emergency food.")

func try_remove(id: String) -> Dictionary:
	if not owned(id): return result(false,"Choose a building or road in your village.")
	var t = tiles[id]
	if t.building == "depot": return result(false,"The village hall is permanent.")
	if t.residents > 0: return result(false,"Keep occupied cottages for their residents.")
	if t.building == "" and not t.road: return result(false,"There is nothing to remove.")
	for cargo in state.shipments:
		if id in cargo.path: return result(false,"Wait for travelling goods to clear this route.")
	state.stock.ore += t.reserved
	t.reserved = 0
	t.work = 0.0
	t.workers = 0
	t.paused = false
	if t.building == "": t.road = false
	else: t.road = true
	t.building = ""
	t.tower_level = 0
	t.surveyed_level = 0
	changed()
	return result(true,"Removed. Construction costs are not refunded; explored land remains known.")

func research_error(kind: String) -> String:
	if not Rules.RESEARCH.has(kind): return "Unknown research."
	if kind in state.research: return "Already researched."
	if state.research_job != "": return "Finish the current research project first."
	if legacy_credit(kind): return ""
	var r = Rules.RESEARCH[kind]
	if state.population < r.population: return "Welcome %d citizens first." % r.population
	for prerequisite in r.requires:
		if not prerequisite in state.research: return "Requires " + Rules.RESEARCH[prerequisite].name + "."
	if kind == "stoneworking" and not stone_discovered(): return "Discover a stone outcrop beyond the frontier first."
	if not can_pay(r.cost): return "Need " + Rules.cost_text(r.cost) + "."
	return ""

func try_research(kind: String) -> Dictionary:
	var error = research_error(kind)
	if error != "": return result(false,error)
	if legacy_credit(kind):
		state.legacy_research_points -= 1
		state.research.append(kind)
		revision += 1
		return result(true,"Legacy research point used for " + Rules.RESEARCH[kind].name + ".")
	pay(Rules.RESEARCH[kind].cost)
	state.research_job = kind
	state.research_progress = 0.0
	revision += 1
	return result(true,Rules.RESEARCH[kind].name + " started. Materials committed once.")

func legacy_credit(kind: String) -> bool:
	return kind in ["farming","reforestation","deep_mining"] and state.get("legacy_research_points",0)>0

func try_upgrade(id: String) -> Dictionary:
	if not owned(id) or tiles[id].building != "watchtower": return result(false,"Choose a watchtower.")
	if tiles[id].tower_level >= 2: return result(false,"This tower is already reinforced.")
	if not "masonry" in state.research: return result(false,"Research Frontier masonry first.")
	if path(id).is_empty(): return result(false,"Connect the tower to the village first.")
	var cost = {"timber":12,"metal":4,"stone":16}
	if not can_pay(cost): return result(false,"Reinforcement needs " + Rules.cost_text(cost) + ".")
	pay(cost)
	tiles[id].tower_level = 2
	state.expansions += 1
	changed()
	return result(true,"Stone foundations, a wider horizon. The frontier grows.")

func stone_discovered() -> bool:
	for t in tiles.values():
		if t.initial_stone > 0: return true
	return false

func changed() -> void:
	network_dirty = true
	revision += 1
	ensure_network()
	for id in tiles.keys():
		var t = tiles[id]
		if t.building == "watchtower" and t.surveyed_level < t.tower_level and routes.has(id):
			t.surveyed_level = t.tower_level
			reveal(Rules.coord(id),4 if t.tower_level == 1 else 6)

func ensure_network() -> void:
	if not network_dirty: return
	routes.clear()
	routes["0,0"] = ""
	var queue = ["0,0"]
	var index = 0
	while index < queue.size():
		var id = queue[index]
		index += 1
		for other in Rules.neighbours(id):
			if routes.has(other) or not owned(other): continue
			if tiles[other].road or tiles[other].building != "":
				routes[other] = id
				queue.append(other)
	network_dirty = false

func path(id: String) -> Array:
	ensure_network()
	if not routes.has(id): return []
	var out = []
	var current = id
	while current != "":
		out.append(current)
		current = routes[current]
	return out

func shipment(id: String) -> Dictionary:
	for cargo in state.shipments:
		if cargo.source == id: return cargo
	return {}

func capacity() -> int:
	var count = 4
	for t in tiles.values():
		if t.building == "house": count += 4
	return count

func vacant_home() -> String:
	for id in tiles:
		if tiles[id].building == "house" and tiles[id].residents < 4 and not path(id).is_empty(): return id
	return ""

func migration_status() -> String:
	if vacant_home() == "": return "Build a connected cottage to welcome newcomers."
	if state.hungry: return "Supply the next meal to welcome newcomers."
	if state.stock.food < state.population + 1: return "Store %d food to support a newcomer." % (state.population+1)
	return "Next newcomer in %d s · homes fill gradually" % ceili(Rules.MIGRATION_SECONDS-state.migration)

func step(delta: float) -> void:
	if not is_finite(delta) or delta < 0 or delta > 86400: return
	state.remainder += delta
	while state.remainder + 0.00000001 >= Rules.STEP:
		state.remainder = maxf(0.0,state.remainder-Rules.STEP)
		tick()

func tick() -> void:
	var dt = Rules.STEP
	state.clock += dt
	state.forage = maxf(0.0,state.forage-dt)
	state.meal += dt
	if state.meal >= Rules.MEAL_SECONDS:
		state.meal -= Rules.MEAL_SECONDS
		var demand = ceili(float(state.population)/2.0)
		state.hungry = state.stock.food < demand
		state.stock.food = maxi(0,state.stock.food-demand)
	var home = vacant_home()
	if home != "" and not state.hungry and state.stock.food >= state.population+1:
		state.migration += dt
		if state.migration >= Rules.MIGRATION_SECONDS:
			state.migration -= Rules.MIGRATION_SECONDS
			tiles[home].residents += 1
			state.population += 1
			state.stock.food -= 2
			revision += 1
	if state.research_job != "":
		state.research_progress += dt
		if state.research_progress >= Rules.RESEARCH[state.research_job].seconds:
			state.research.append(state.research_job)
			state.research_job = ""
			state.research_progress = 0.0
			revision += 1
	var moving = state.shipments.size()
	for id in tiles:
		var t = tiles[id]
		if t.sapling >= 0:
			t.sapling += dt
			if t.sapling >= Rules.TREE_SECONDS:
				t.sapling = -1.0
				t.trees += 1
				t.terrain = "forest"
				revision += 1
		if t.workers == 0 or t.paused or not Rules.BUILDINGS.has(t.building) or not shipment(id).is_empty(): continue
		var route = path(id)
		if route.is_empty(): continue
		var rule = Rules.BUILDINGS[t.building]
		if t.building == "forester":
			var planting = planting_site(id)
			if planting == "": continue
			t.work += dt * Rules.crew_speed(t.workers)
			if t.work >= rule.seconds:
				t.work = 0.0
				tiles[planting].sapling = 0.0
				revision += 1
			continue
		if depleted(t): continue
		if not can_store(rule.output,rule.amount): continue
		if t.building == "smelter" and t.reserved == 0:
			if state.stock.ore < 2: continue
			state.stock.ore -= 2
			t.reserved = 2
		t.work += dt * Rules.crew_speed(t.workers)
		if t.work < rule.seconds: continue
		t.work = 0.0
		var amount = int(rule.amount)
		match t.building:
			"logging":
				t.trees -= 1
				if t.trees == 0: t.terrain = "grass"
			"mine":
				amount = mini(amount,t.ore)
				t.ore -= amount
			"deep_mine":
				amount = mini(amount,t.deep)
				t.deep -= amount
			"quarry":
				amount = mini(amount,t.stone)
				t.stone -= amount
			"smelter": t.reserved = 0
		state.shipments.append({"source":id,"resource":rule.output,"amount":amount,
			"elapsed":0.0,"duration":maxf(Rules.DELIVERY_SECONDS,(route.size()-1)*Rules.DELIVERY_SECONDS),"path":route})
		revision += 1
	for i in range(moving-1,-1,-1):
		var cargo = state.shipments[i]
		cargo.elapsed += dt
		if cargo.elapsed + 0.000001 >= cargo.duration:
			state.stock[cargo.resource] += cargo.amount
			state.shipments.remove_at(i)
	for id in tiles: cleanup(id)

func depleted(t: Dictionary) -> bool:
	return (t.building == "logging" and t.trees == 0) or (t.building == "mine" and t.ore == 0) or (t.building == "deep_mine" and t.deep == 0) or (t.building == "quarry" and t.stone == 0)

func managed(id: String) -> bool:
	for other in Rules.neighbours(id):
		if owned(other) and tiles[other].building == "forester": return true
	return false

func cleanup(id: String) -> void:
	var t = tiles[id]
	if not depleted(t) or not shipment(id).is_empty(): return
	if t.building == "logging" and managed(id): return
	t.building = ""
	t.road = true
	t.workers = 0
	t.work = 0.0
	t.paused = false
	network_dirty = true
	revision += 1

func planting_site(id: String) -> String:
	var best = ""
	for other in Rules.neighbours(id):
		if not owned(other): continue
		var t = tiles[other]
		if t.terrain not in ["grass","forest"] or t.building not in ["","logging"] or t.initial_ore > 0 or t.initial_stone > 0 or t.trees >= 12 or t.sapling >= 0: continue
		if best == "" or t.trees < tiles[best].trees or (t.trees==tiles[best].trees and t.building=="logging" and tiles[best].building!="logging"): best = other
	return best

func work_status(id: String) -> String:
	var t = tiles[id]
	if t.building == "house": return "%d / 4 residents\n%s" % [t.residents,migration_status()]
	if t.building == "watchtower": return "Survey radius: %d hexes\n%s" % [4 if t.tower_level == 1 else 6,"Land stays known after discovery." if not path(id).is_empty() else "Connect a road to survey this frontier."]
	if t.building == "depot": return "Shared stores for the village.\nGoods arrive here before they can be spent."
	if t.building == "": return "Claim and develop this plot." if not t.claimed else "Ready for a new purpose."
	var cargo = shipment(id)
	if not cargo.is_empty(): return "Delivering %d %s · %d s\nThis crew waits for its cart." % [cargo.amount,cargo.resource,ceili(cargo.duration-cargo.elapsed)]
	if t.paused: return "Work paused · crew released."
	if path(id).is_empty(): return "Needs a road connection to the village hall."
	if t.workers == 0: return "No crew assigned. Welcome or reassign a citizen."
	if depleted(t): return "Waiting for woodland regrowth." if t.building == "logging" else "Deposit exhausted."
	var output_rule = Rules.BUILDINGS[t.building]
	if output_rule.output!="" and not can_store(output_rule.output,output_rule.amount): return "Village stores are full. Spend supplies to resume work."
	if t.building == "smelter" and t.reserved == 0 and state.stock.ore < 2: return "Waiting for 2 ore in village stores."
	if t.building == "forester" and planting_site(id) == "": return "No neighbouring plot ready for planting."
	var rule = Rules.BUILDINGS[t.building]
	return "%d workers · %.0f%% work speed\n%s" % [t.workers,Rules.crew_speed(t.workers)*100,"2 ore reserved for this job." if t.reserved > 0 else rule.detail]

func progress(id: String) -> float:
	var cargo = shipment(id)
	if not cargo.is_empty(): return float(cargo.elapsed)/cargo.duration
	var t = tiles[id]
	return float(t.work)/Rules.BUILDINGS.get(t.building,{}).get("seconds",1.0)

func guidance() -> String:
	if state.hungry: return "Food ran short. A forager crew or hand foraging can restore the next meal."
	if state.stock.timber<12 or state.stock.metal<2: return "Room to expand · keep 6 timber and 1 metal for your next resource claim."
	if state.population < 6: return "Room to belong · build a cottage, support new arrivals, and assign their work."
	if not "surveying" in state.research: return "Beyond the treeline · research Frontier surveying to open new horizons."
	if not stone_discovered(): return "A wider horizon · build a connected watchtower near your frontier."
	if not "quarry_tools" in state.research: return "Stone discovered · research Stoneworking and Quarrying tools to put it to use."
	if state.expansions == 0: return "Build with what you find · quarry stone, research masonry, and reinforce a watchtower."
	return "The frontier is open · build new outposts, balance your crews, and choose your next direction."

func record() -> Dictionary:
	var out = state.duplicate(true)
	out.tiles = []
	# Stable discovery order is also the tie-breaker for migration and jobs.
	for id in tiles: out.tiles.append(tiles[id].duplicate(true))
	return out

func restore(record_value: Dictionary) -> String:
	var error = validate(record_value)
	if error != "": return error
	var copy = record_value.duplicate(true)
	# JSON numbers arrive as floats. Restore validated count fields to integers.
	for field in ["schema","seed","population","expansions","legacy_research_points"]: copy[field] = int(copy[field])
	for resource in Rules.RESOURCES: copy.stock[resource] = int(copy.stock[resource])
	for cargo in copy.shipments: cargo.amount = int(cargo.amount)
	var next_tiles = {}
	for t in copy.tiles:
		for field in ["q","r","height","trees","ore","initial_ore","deep","initial_deep","stone","initial_stone","workers","residents","reserved","tower_level","surveyed_level"]:
			t[field] = int(t[field])
		next_tiles[Rules.key(Vector2i(int(t.q),int(t.r)))] = t
	copy.erase("tiles")
	state = copy
	tiles = next_tiles
	network_dirty = true
	revision += 1
	return ""

static func number(value, low: float, high: float, whole := false) -> bool:
	return (value is int or value is float) and is_finite(float(value)) and value >= low and value <= high and (not whole or float(value) == floorf(value))

static func validate(s: Dictionary) -> String:
	if s.get("schema") != Rules.SCHEMA: return "Unsupported save version."
	if not number(s.get("seed"),-2147483648,2147483647,true): return "Invalid seed."
	for field in ["population","expansions","legacy_research_points"]:
		if not number(s.get(field),0,2147483647,true): return "Invalid " + field + "."
	if s.population < 4: return "Invalid population."
	if s.legacy_research_points>3: return "Invalid legacy research credits."
	for field in ["clock","remainder","migration","meal","forage","research_progress"]:
		if not number(s.get(field),0,1e12): return "Invalid clock: " + field
	if s.remainder >= Rules.STEP or s.migration >= Rules.MIGRATION_SECONDS or s.meal >= Rules.MEAL_SECONDS or s.forage > Rules.FORAGE_SECONDS: return "Invalid timer range."
	if not s.get("hungry") is bool or not s.get("stock") is Dictionary: return "Missing town state."
	for resource in Rules.RESOURCES:
		if not number(s.stock.get(resource),0,Rules.STOCK_LIMIT,true): return "Invalid stock."
	if not s.get("research") is Array or not s.get("research_job") is String: return "Invalid research state."
	var seen_research = {}
	for kind in s.research:
		if not kind is String or not Rules.RESEARCH.has(kind) or seen_research.has(kind): return "Invalid research unlock."
		seen_research[kind] = true
	for kind in seen_research:
		for required in Rules.RESEARCH[kind].requires:
			if not seen_research.has(required): return "Missing research prerequisite."
	if s.research_job != "":
		if not Rules.RESEARCH.has(s.research_job) or s.research_job in s.research: return "Invalid research job."
		var rule = Rules.RESEARCH[s.research_job]
		if s.research_progress >= rule.seconds or s.population < rule.population: return "Invalid research progress."
		for required in rule.requires:
			if not required in s.research: return "Invalid active research prerequisite."
	elif s.research_progress != 0: return "Research progress without a project."
	if not s.get("tiles") is Array or s.tiles.is_empty() or s.tiles.size() > 1+3*Rules.WORLD_LIMIT*(Rules.WORLD_LIMIT+1): return "Invalid region size."
	var map = {}
	var population = 4
	var workforce = 0
	for t in s.tiles:
		if not t is Dictionary: return "Invalid tile."
		for field in ["q","r"]:
			if not number(t.get(field),-Rules.WORLD_LIMIT,Rules.WORLD_LIMIT,true): return "Invalid coordinate."
		var coord_value = Vector2i(t.q,t.r)
		var id = Rules.key(coord_value)
		if Rules.distance(coord_value)>Rules.WORLD_LIMIT or map.has(id): return "Duplicate or out-of-range tile."
		map[id] = t
		if t.get("terrain") not in ["grass","forest","ore","rock","stone","sand","water"]: return "Invalid terrain."
		if not t.get("building") is String or (t.building != "" and t.building != "depot" and not Rules.BUILDINGS.has(t.building)): return "Invalid building."
		for field in ["claimed","road","paused"]:
			if not t.get(field) is bool: return "Invalid tile flag."
		for field in ["height","trees","ore","initial_ore","deep","initial_deep","stone","initial_stone","workers","residents","reserved","tower_level","surveyed_level"]:
			if not number(t.get(field),0,1000000,true): return "Invalid tile accounting: " + field
		if t.height>5 or t.residents>4 or int(t.reserved) not in [0,2] or t.tower_level>2 or t.surveyed_level>t.tower_level: return "Invalid tile limits."
		if t.ore>t.initial_ore or t.deep>t.initial_deep or t.stone>t.initial_stone: return "Deposit increased beyond its original reserve."
		if t.initial_deep!=t.initial_ore*2 or (t.initial_ore>0 and t.initial_stone>0): return "Inconsistent finite reserves."
		if t.trees>64 or (t.trees>0)!=(t.terrain=="forest"): return "Inconsistent woodland."
		if t.ore>0 and t.terrain!="ore": return "Iron on unrelated terrain."
		if t.initial_ore>0 and t.terrain not in ["ore","rock"]: return "Iron history on unrelated terrain."
		if t.initial_stone>0 and t.terrain!="stone": return "Stone history on unrelated terrain."
		if t.deep<t.initial_deep and (t.ore>0 or not "deep_mining" in s.research): return "Invalid deep extraction."
		if not number(t.get("work"),0,1e6) or not number(t.get("sapling"),-1,Rules.TREE_SECONDS): return "Invalid work or growth timer."
		if t.sapling>=Rules.TREE_SECONDS: return "Sapling growth out of range."
		if t.terrain=="water" and (t.claimed or t.road or t.building!=""): return "Water cannot be developed."
		if (t.building!="" or t.road) and not t.claimed: return "Unowned development."
		if t.building=="depot" and id!="0,0": return "Duplicate village hall."
		if t.residents>0 and t.building!="house": return "Citizens without a home."
		if t.reserved>0 and t.building!="smelter": return "Ore reserved by a non-foundry."
		if t.building=="smelter" and t.work>0 and t.reserved!=2: return "Foundry work has no reserved input."
		if t.building!="watchtower" and (t.tower_level>0 or t.surveyed_level>0): return "Invalid survey structure."
		if t.building=="watchtower" and (t.tower_level<1 or (t.tower_level==2 and not "masonry" in s.research)): return "Invalid watchtower upgrade."
		var building_rule = Rules.BUILDINGS.get(t.building,{})
		if t.workers>building_rule.get("crew",0) or (t.paused and t.workers>0): return "Invalid crew."
		if t.paused and building_rule.get("crew",0)==0: return "Only workplaces can pause."
		if t.work>=building_rule.get("seconds",1) or (t.work>0 and building_rule.get("crew",0)==0): return "Invalid workplace progress."
		if t.building in ["depot","house","smelter","watchtower","farm","forester"] and (t.trees>0 or t.ore>0 or t.stone>0 or t.sapling>=0): return "Building needs clear ground."
		if t.building in ["farm","forester"] and (t.terrain!="grass" or t.initial_ore>0): return "Building needs grassland."
		if t.building in ["logging","forager"] and t.terrain not in ["grass","forest"]: return "Workplace needs woodland or meadow."
		if t.building in ["mine","deep_mine"] and t.initial_ore==0: return "Mine has no deposit."
		if t.building=="deep_mine" and t.ore>0: return "Surface iron is not exhausted."
		if t.building=="quarry" and t.initial_stone==0: return "Quarry has no stone deposit."
		if t.sapling>=0 and (not t.claimed or not "reforestation" in s.research or t.initial_ore>0 or t.initial_stone>0 or t.trees>=12 or t.terrain not in ["grass","forest"] or t.building not in ["","logging"]): return "Invalid planted sapling."
		var research = building_rule.get("research","")
		if research!="" and not research in s.research: return "Building research is missing."
		population += int(t.residents)
		workforce += int(t.workers)
	if not map.has("0,0") or map["0,0"].building!="depot": return "Village hall missing."
	if population!=s.population or workforce>s.population: return "Population or worker accounting is inconsistent."
	var connected = {"0,0":true}
	var frontier = ["0,0"]
	var cursor = 0
	while cursor<frontier.size():
		for other in Rules.neighbours(frontier[cursor]):
			if map.has(other) and map[other].claimed and not connected.has(other):
				connected[other] = true
				frontier.append(other)
		cursor += 1
	for id in map:
		if map[id].claimed and not connected.has(id): return "Disconnected claimed territory."
	if not s.get("shipments") is Array or s.shipments.size()>s.tiles.size(): return "Invalid cargo list."
	var sources = {}
	var owned_goods = s.stock.duplicate()
	for t in map.values(): owned_goods.ore += t.reserved
	for cargo in s.shipments:
		if not cargo is Dictionary or cargo.get("resource") not in Rules.RESOURCES or not number(cargo.get("amount"),1,100000,true): return "Invalid cargo."
		if not cargo.get("path") is Array or cargo.path.size()<2 or cargo.path.size()>s.tiles.size(): return "Invalid route."
		if not cargo.get("source") is String or cargo.source!=cargo.path[0] or cargo.path[-1]!="0,0" or sources.has(cargo.source): return "Invalid cargo source."
		if not map.has(cargo.source): return "Cargo source missing."
		var rule = Rules.BUILDINGS.get(map[cargo.source].building,{})
		if rule.get("output","")!=cargo.resource: return "Cargo does not match its producer."
		if cargo.amount>rule.amount: return "Cargo exceeds the producer's batch."
		if cargo.resource in ["timber","metal"] and cargo.amount!=rule.amount: return "Invalid cargo batch."
		if map[cargo.source].work!=0 or map[cargo.source].reserved!=0: return "Producer works while its cargo travels."
		sources[cargo.source] = true
		if not number(cargo.get("elapsed"),0,1e9) or not number(cargo.get("duration"),0.001,1e9) or cargo.elapsed>=cargo.duration: return "Invalid delivery time."
		var edges = cargo.path.size()-1
		if cargo.duration!=edges*Rules.DELIVERY_SECONDS and cargo.duration!=edges*0.75: return "Delivery time does not match its road."
		owned_goods[cargo.resource] += cargo.amount
		var previous = ""
		var visited = {}
		for id in cargo.path:
			if not id is String or not map.has(id) or visited.has(id) or not map[id].claimed or (map[id].building=="" and not map[id].road): return "Broken cargo route."
			if previous!="" and Rules.distance(Rules.coord(previous),Rules.coord(id))!=1: return "Non-adjacent cargo route."
			visited[id] = true
			previous = id
	for resource in Rules.RESOURCES:
		if owned_goods[resource]>Rules.STOCK_LIMIT: return "Stored and committed goods exceed capacity."
	return ""
