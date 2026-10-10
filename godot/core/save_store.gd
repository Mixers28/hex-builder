class_name FrontierSaveStore
extends RefCounted

const Sim = preload("res://core/simulation.gd")
const Rules = preload("res://core/rules.gd")
const PRIMARY = "user://frontier.json"
const LEGACY_BUILDINGS = ["","depot","logging","mine","smelter","house","forager","farm","forester","deep_mine"]
const LEGACY_TERRAINS = ["grass","forest","rock","ore","sand","water"]

static func read(path: String) -> Dictionary:
	if not FileAccess.file_exists(path): return {"ok":false,"error":"Save file not found."}
	var file = FileAccess.open(path,FileAccess.READ)
	if file == null: return {"ok":false,"error":"Could not read the save file."}
	if file.get_length()>8*1024*1024: return {"ok":false,"error":"Save file is too large."}
	var json = JSON.new()
	if json.parse(file.get_as_text())!=OK or not json.data is Dictionary:
		return {"ok":false,"error":"Invalid JSON save. The original file was retained."}
	var data = json.data
	var imported = data.has("SchemaVersion")
	if imported:
		var converted = import_unity(data)
		if not converted.ok: return converted
		data = converted.record
	var sim = Sim.new()
	var error = sim.restore(data)
	if error!="": return {"ok":false,"error":error}
	return {"ok":true,"simulation":sim,"imported":imported}

static func load_with_backup(path: String = PRIMARY) -> Dictionary:
	var loaded = read(path)
	if loaded.ok: return loaded
	var backup = read(path+".bak")
	if backup.ok:
		backup["recovered"] = true
		return backup
	return loaded

static func write(sim, path: String = PRIMARY) -> String:
	var record = sim.record()
	var error = Sim.validate(record)
	if error!="": return "Save rejected: " + error
	var temporary = path+".pending"
	var file = FileAccess.open(temporary,FileAccess.WRITE)
	if file==null: return "Could not open a temporary save file."
	file.store_string(JSON.stringify(record,"",true,true))
	file.flush()
	var write_error = file.get_error()
	file.close()
	if write_error!=OK: return "Writing the save failed; the previous save remains intact."
	if FileAccess.file_exists(path):
		var prior = read(path)
		# Never replace a good backup with a corrupt primary.
		if prior.ok and DirAccess.copy_absolute(path,path+".bak")!=OK:
			return "Could not preserve the previous save."
	if DirAccess.rename_absolute(temporary,path)!=OK:
		return "Could not replace the save. The previous file was retained."
	return ""

static func import_unity(old: Dictionary) -> Dictionary:
	var bad = {"ok":false,"error":"The Unity save is invalid or unsupported. Its original file was retained."}
	if not Sim.number(old.get("SchemaVersion"),1,2,true): return bad
	var version = int(old.SchemaVersion)
	if not old.get("Tiles") is Array or not old.get("Stock") is Dictionary or not old.get("Shipments") is Array: return bad
	if not Sim.number(old.get("Seed"),-2147483648,2147483647,true) or old.Tiles.size()!=127: return bad
	for resource in ["Timber","Ore","Metal"]:
		if not Sim.number(old.Stock.get(resource),0,Rules.STOCK_LIMIT,true): return bad
	var base = Sim.new()
	base.start(int(old.Seed))
	var s = base.record()
	s.tiles = []
	s.shipments = []
	s.clock = old.get("ElapsedSeconds",0.0)
	s.remainder = old.get("PendingSeconds",0.0)
	s.stock = {"timber":old.Stock.Timber,"ore":old.Stock.Ore,"metal":old.Stock.Metal,"food":24,"stone":0}
	var town = old.get("Town",{})
	if version==2:
		if not town is Dictionary or not Sim.number(town.get("Population"),4,508,true) or not Sim.number(town.get("Research"),0,7,true): return bad
		if not Sim.number(town.get("EarnedMilestones"),0,3,true) or not Sim.number(old.Stock.get("Food"),0,Rules.STOCK_LIMIT,true): return bad
		s.population = town.Population
		s.stock.food = old.Stock.Food
		s.migration = town.get("ImmigrationProgress",0.0)
		s.meal = town.get("MealProgress",0.0)
		s.forage = town.get("ForageCooldown",0.0)
		s.hungry = town.get("LastMealMissed",false)
		for item in [[1,"farming"],[2,"reforestation"],[4,"deep_mining"]]:
			if int(town.Research)&item[0]: s.research.append(item[1])
		var milestones = 3 if s.population>=14 else 2 if s.population>=10 else 1 if s.population>=6 else 0
		if town.EarnedMilestones!=milestones or s.research.size()>milestones: return bad
		s.legacy_research_points = milestones-s.research.size()
	var index = {}
	for item in old.Tiles:
		if not item is Dictionary or not item.get("Coord") is Dictionary: return bad
		var c = item.Coord
		if not Sim.number(c.get("Q"),-6,6,true) or not Sim.number(c.get("R"),-6,6,true): return bad
		var coordinate = Vector2i(c.Q,c.R)
		var id = Rules.key(coordinate)
		if Rules.distance(coordinate)>6 or index.has(id): return bad
		if not Sim.number(item.get("Terrain"),0,5,true) or not Sim.number(item.get("Building"),0,9 if version==2 else 4,true): return bad
		var t = base.generate(coordinate)
		t.terrain = LEGACY_TERRAINS[int(item.Terrain)]
		t.building = LEGACY_BUILDINGS[int(item.Building)]
		t.height = item.get("Elevation",0)
		t.claimed = item.get("Claimed",false)
		t.road = item.get("HasRoad",false)
		t.trees = item.get("TreesRemaining",0)
		t.ore = item.get("OreRemaining",0)
		t.initial_ore = item.get("InitialOre",0)
		if not Sim.number(t.initial_ore,0,1000000,true): return bad
		t.deep = item.get("DeepOreRemaining",0) if version==2 else t.initial_ore*2
		t.initial_deep = item.get("DeepOreInitial",0) if version==2 else t.initial_ore*2
		t.stone = 0
		t.initial_stone = 0
		t.residents = item.get("Residents",0) if version==2 else 0
		t.paused = item.get("WorkPaused",false) if version==2 else false
		t.reserved = item.get("InputOreReserved",0)
		t.work = item.get("WorkProgress",0.0)
		# Preserve the fraction completed when the new production clock is longer.
		var old_seconds = {"logging":4.0,"mine":5.0,"smelter":6.0,"forager":8.0,"farm":20.0,"forester":8.0,"deep_mine":7.0}
		if not Sim.number(t.work,0,old_seconds.get(t.building,1.0)): return bad
		if old_seconds.has(t.building):
			if t.work>=old_seconds[t.building]: return bad
			t.work = t.work/old_seconds[t.building]*Rules.BUILDINGS[t.building].seconds
		var sapling = item.get("SaplingPlanted",false) if version==2 else false
		if not sapling is bool: return bad
		if not Sim.number(item.get("SaplingAge",0.0),0,45.0): return bad
		t.sapling = item.get("SaplingAge",0.0)*2.0 if sapling else -1.0
		index[id] = t
		s.tiles.append(t)
	# Reconstruct the old food-first, coordinate-ordered assignments once.
	var ordered = s.tiles.duplicate()
	ordered.sort_custom(func(a,b): return a.q<b.q if a.q!=b.q else a.r<b.r)
	base.tiles = index
	base.network_dirty = true
	var available = int(s.population)
	for food_pass in [true,false]:
		for t in ordered:
			var food = t.building in ["forager","farm"]
			if food!=food_pass or available==0 or t.paused or Rules.BUILDINGS.get(t.building,{}).get("crew",0)==0: continue
			if base.path(Rules.key(Vector2i(t.q,t.r))).is_empty(): continue
			t.workers = 1
			available -= 1
	for item in old.Shipments:
		if not item is Dictionary or not item.get("Path") is Array or not Sim.number(item.get("Resource"),0,3,true): return bad
		var route = []
		for c in item.Path:
			if not c is Dictionary or not Sim.number(c.get("Q"),-6,6,true) or not Sim.number(c.get("R"),-6,6,true): return bad
			route.append(Rules.key(Vector2i(c.Q,c.R)))
		if route.is_empty(): return bad
		s.shipments.append({"source":route[0],"resource":Rules.RESOURCES[int(item.Resource)],"amount":item.get("Amount",0),"elapsed":item.get("Elapsed",0.0),"duration":item.get("Duration",0.0),"path":route})
	var validation = Sim.validate(s)
	if validation!="": return {"ok":false,"error":"Unity import rejected: "+validation}
	return {"ok":true,"record":s}
