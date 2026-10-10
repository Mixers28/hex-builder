extends RefCounted

# Serialization fixture for the documented Unity formats, not another simulation.
static func make(version: int=2) -> Dictionary:
	var old = {"SchemaVersion":version,"Seed":417,"ElapsedSeconds":24.0,"SimulationSteps":192,"PendingSeconds":0.0625,
		"Stock":{"Timber":37,"Ore":9,"Metal":5,"Food":29},"Tiles":[],"Shipments":[],
		"Town":{"Population":6,"EarnedMilestones":1,"Research":1,"ImmigrationProgress":7.5,"MealProgress":24.0,"ForageCooldown":2.0,"LastMealMissed":false}}
	for q in range(-6,7):
		for r in range(-6,7):
			if (absi(q)+absi(r)+absi(q+r))/2>6: continue
			var d = (absi(q)+absi(r)+absi(q+r))/2
			var t = {"Coord":{"Q":q,"R":r},"Terrain":0,"Elevation":0,"Claimed":d<=1,"HasRoad":false,"Building":0,
				"TreesRemaining":0,"OreRemaining":0,"InitialOre":0,"ExcavationDepth":0,"WorkProgress":0.0,"InputOreReserved":0,
				"Residents":0,"WorkPaused":false,"DeepOreInitial":0,"DeepOreRemaining":0,"SaplingPlanted":false,"SaplingAge":0.0}
			if q==0 and r==0: t.Building=1; t.HasRoad=true
			if q==-1 and r==0: t.Terrain=1; t.TreesRemaining=9; t.Building=2
			if q==1 and r==0: t.Terrain=3; t.OreRemaining=40; t.InitialOre=40; t.DeepOreInitial=80; t.DeepOreRemaining=80; t.Building=3; t.WorkProgress=2.0
			if q==1 and r==-1: t.Building=4; t.InputOreReserved=2; t.WorkProgress=3.0
			if version==2:
				if q==0 and r==1: t.Building=5; t.Residents=2
				if q==-1 and r==1: t.Building=6; t.WorkProgress=2.0
			old.Tiles.append(t)
	old.Shipments.append({"Resource":0,"Amount":3,"Elapsed":0.25,"Duration":0.75,"Path":[{"Q":-1,"R":0},{"Q":0,"R":0}]})
	if version==1:
		old.erase("Town")
		old.Stock.erase("Food")
		for t in old.Tiles:
			for field in ["Residents","WorkPaused","DeepOreInitial","DeepOreRemaining","SaplingPlanted","SaplingAge"]: t.erase(field)
	return old
