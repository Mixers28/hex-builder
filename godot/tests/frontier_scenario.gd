extends RefCounted

const Sim = preload("res://core/simulation.gd")
const Rules = preload("res://core/rules.gd")
var sim
var error = ""
var snapshots = {}
var commands = 0

func accept(outcome: Dictionary, context: String) -> bool:
	commands += 1
	if not outcome.ok: error = context+": "+outcome.message
	return outcome.ok

func gather_basic(resource: String) -> void:
	# Exercise the same recovery commands available to a player; never award stock.
	var field = "trees" if resource=="timber" else "ore"
	for id in sim.tiles:
		if sim.owned(id) and sim.tiles[id][field]>0:
			accept(sim.try_gather(id),"Hand gathering")
			return
	if not sim.can_pay({"timber":6,"metal":1}): return
	for id in sim.tiles:
		if sim.tiles[id].claimed or sim.tiles[id][field]==0: continue
		for other in Rules.neighbours(id):
			if sim.owned(other):
				if accept(sim.try_claim(id),"Recovery resource claim"):
					accept(sim.try_gather(id),"New resource gathering")
				return

func advance(seconds: float) -> void:
	sim.step(seconds)

func fund(cost: Dictionary) -> bool:
	# Keep the price of a neighbouring resource claim in reserve, as a player can.
	var target = cost.duplicate()
	target.timber = target.get("timber",0)+6
	target.metal = target.get("metal",0)+1
	for i in range(1800):
		if sim.can_pay(target): return true
		if sim.state.stock.timber<target.timber: gather_basic("timber")
		if sim.state.stock.ore<2: gather_basic("ore")
		if error!="": return false
		advance(1)
	error = "Economy could not fund "+Rules.cost_text(cost)
	return false

func build(id: String, kind: String, crew: int=0) -> bool:
	if not fund(Rules.BUILDINGS[kind].cost): return false
	if not accept(sim.try_build(id,kind),"Build "+kind): return false
	return crew==0 or accept(sim.try_workers(id,crew),"Assign "+kind)

func claim(id: String) -> bool:
	if not fund({"timber":6,"metal":1}): return false
	return accept(sim.try_claim(id),"Claim "+id)

func road(id: String) -> bool:
	return fund({"timber":2}) and accept(sim.try_road(id),"Road "+id)

func wait_population(count: int) -> bool:
	for i in range(1500):
		if sim.state.population>=count: return true
		advance(1)
	error = "Population did not reach "+str(count)
	return false

func research(kind: String) -> bool:
	if not wait_population(Rules.RESEARCH[kind].population) or not fund(Rules.RESEARCH[kind].cost): return false
	if not accept(sim.try_research(kind),"Research "+kind): return false
	advance(Rules.RESEARCH[kind].seconds)
	return kind in sim.state.research

func run(seed_value: int=417) -> Dictionary:
	sim = Sim.new()
	sim.start(seed_value)
	error = ""
	snapshots.clear()
	commands = 0
	snapshots["start"] = sim.record()
	if not build("0,1","house"): return failed()
	if not build("-1,1","forager",1): return failed()
	if not build("-1,0","logging",1): return failed()
	if not build("1,0","mine",1): return failed()
	if not build("1,-1","smelter",1): return failed()
	advance(45.25)
	snapshots["village"] = sim.record()
	if not build("0,-1","house"): return failed()
	if not research("surveying"): return failed()
	if not claim("0,2") or not build("0,2","watchtower"): return failed()
	snapshots["discovery"] = sim.record()
	if not sim.tiles.has("0,5") or sim.tiles["0,5"].claimed:
		error = "Tower did not reveal an unclaimed stone deposit"
		return failed()
	if not research("stoneworking") or not research("quarry_tools"): return failed()
	if not claim("0,3") or not road("0,3"): return failed()
	if not claim("0,4") or not road("0,4"): return failed()
	if not claim("0,5") or not build("0,5","quarry",2): return failed()
	if not accept(sim.try_workers("1,-1",2),"Reallocate foundry crew"): return failed()
	for i in range(200):
		advance(Rules.STEP)
		if not sim.shipment("0,5").is_empty(): break
	if sim.shipment("0,5").is_empty():
		error = "The staffed quarry did not dispatch stone"
		return failed()
	snapshots["extraction"] = sim.record()
	if not research("masonry") or not fund({"timber":12,"metal":4,"stone":16}): return failed()
	var known = sim.tiles.size()
	if not accept(sim.try_upgrade("0,2"),"Reinforce watchtower"): return failed()
	if sim.tiles.size()<=known:
		error = "Reinforced tower did not open another frontier"
		return failed()
	snapshots["expansion"] = sim.record()
	var validation = Sim.validate(sim.record())
	if validation!="": error = validation; return failed()
	return {"ok":true,"simulation":sim,"snapshots":snapshots,"commands":commands,"seconds":sim.state.clock}

func failed() -> Dictionary:
	return {"ok":false,"error":error,"commands":commands,"seconds":sim.state.clock,"stock":sim.state.stock}
