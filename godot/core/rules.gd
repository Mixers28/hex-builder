class_name FrontierRules
extends RefCounted

const SCHEMA = 3
const STEP = 0.125
const WORLD_LIMIT = 16
const START_REVEAL = 3
const MIGRATION_SECONDS = 90.0
const MEAL_SECONDS = 30.0
const FORAGE_SECONDS = 8.0
const TREE_SECONDS = 90.0
const DELIVERY_SECONDS = 1.25
const STOCK_LIMIT = 2147483647
const RESOURCES = ["timber", "ore", "metal", "food", "stone"]
const DIRECTIONS = [Vector2i(1,0), Vector2i(1,-1), Vector2i(0,-1), Vector2i(-1,0), Vector2i(-1,1), Vector2i(0,1)]

const BUILDINGS = {
	"house": {"name":"Cottage", "cost":{"timber":10}, "crew":0, "research":"", "detail":"Four places for people arriving over time."},
	"forager": {"name":"Forager lodge", "cost":{"timber":8}, "crew":2, "research":"", "seconds":12.0, "output":"food", "amount":3, "detail":"Gather food. Each crew shares one delivery cart."},
	"logging": {"name":"Woodcutter", "cost":{"timber":12}, "crew":3, "research":"", "seconds":12.0, "output":"timber", "amount":3, "detail":"One tree becomes three timber; needs woodland."},
	"mine": {"name":"Iron mine", "cost":{"timber":16,"metal":2}, "crew":3, "research":"", "seconds":15.0, "output":"ore", "amount":2, "detail":"Extract finite surface ore from an iron deposit."},
	"smelter": {"name":"Foundry", "cost":{"timber":20,"metal":4}, "crew":3, "research":"", "seconds":18.0, "output":"metal", "amount":1, "detail":"Reserve two ore for each bar of metal."},
	"farm": {"name":"Farmstead", "cost":{"timber":18,"metal":3}, "crew":3, "research":"farming", "seconds":24.0, "output":"food", "amount":6, "detail":"A larger food supply for a growing village."},
	"forester": {"name":"Tree nursery", "cost":{"timber":18,"metal":4}, "crew":2, "research":"reforestation", "seconds":16.0, "output":"", "amount":0, "detail":"Replant neighbouring owned land. Trees mature in 90 s."},
	"deep_mine": {"name":"Deep mine", "cost":{"timber":26,"metal":8}, "crew":3, "research":"deep_mining", "seconds":18.0, "output":"ore", "amount":2, "detail":"Reach the finite reserve below an exhausted iron mine."},
	"watchtower": {"name":"Watchtower", "cost":{"timber":24,"metal":4}, "crew":0, "research":"surveying", "detail":"Survey four hexes around this connected frontier tower."},
	"quarry": {"name":"Stone quarry", "cost":{"timber":22,"metal":6}, "crew":3, "research":"quarry_tools", "seconds":20.0, "output":"stone", "amount":2, "detail":"Use researched tools to quarry a discovered stone outcrop."}
}

const RESEARCH = {
	"farming": {"name":"Cultivation", "cost":{"timber":16,"metal":2}, "seconds":90.0, "population":6, "requires":[], "effect":"Build farmsteads for a larger food supply."},
	"surveying": {"name":"Frontier surveying", "cost":{"timber":16,"metal":3}, "seconds":90.0, "population":6, "requires":[], "effect":"Build watchtowers to reveal new land and deposits."},
	"reforestation": {"name":"Woodland stewardship", "cost":{"timber":18,"metal":4}, "seconds":120.0, "population":8, "requires":[], "effect":"Plant and harvest renewable woodland with a nursery."},
	"stoneworking": {"name":"Stoneworking", "cost":{"timber":12,"metal":4}, "seconds":90.0, "population":6, "requires":["surveying"], "effect":"Understand a discovered stone deposit and unlock tool research."},
	"quarry_tools": {"name":"Quarrying tools", "cost":{"timber":24,"metal":8}, "seconds":150.0, "population":8, "requires":["stoneworking"], "effect":"Build and staff stone quarries. Tools are a permanent capability."},
	"masonry": {"name":"Frontier masonry", "cost":{"timber":20,"metal":6,"stone":10}, "seconds":180.0, "population":10, "requires":["quarry_tools"], "effect":"Reinforce watchtowers with stone to survey six hexes."},
	"deep_mining": {"name":"Deep mining", "cost":{"timber":30,"metal":8}, "seconds":180.0, "population":10, "requires":[], "effect":"Reach deeper finite iron reserves at exhausted surface mines."}
}

static func key(coord: Vector2i) -> String:
	return "%d,%d" % [coord.x, coord.y]

static func coord(id: String) -> Vector2i:
	var parts = id.split(",")
	return Vector2i(int(parts[0]), int(parts[1]))

static func distance(a: Vector2i, b := Vector2i.ZERO) -> int:
	var d = a - b
	return (absi(d.x) + absi(d.y) + absi(d.x + d.y)) / 2

static func neighbours(id: String) -> Array:
	var out = []
	for direction in DIRECTIONS:
		out.append(key(coord(id) + direction))
	return out

static func cost_text(cost: Dictionary) -> String:
	var parts = PackedStringArray()
	for resource in RESOURCES:
		if cost.get(resource, 0) > 0:
			parts.append("%d %s" % [cost[resource], resource])
	return " · ".join(parts)

static func title(kind: String) -> String:
	return "Village hall" if kind == "depot" else BUILDINGS.get(kind, {"name":"Open land"}).name

static func crew_speed(count: int) -> float:
	return 0.0 if count == 0 else 1.0 + 0.55 * (count - 1)
