class_name FrontierWorldView
extends Node3D

const Art = preload("res://runtime/world_art.gd")
const Rules = preload("res://core/rules.gd")
var sim
var material: StandardMaterial3D
var fog_material: StandardMaterial3D
var tile_nodes = {}
var signatures = {}
var fog_nodes = {}
var people: Array = []
var carts: Array = []
var cart_materials = {}
var selection: MeshInstance3D
var selected = ""
var last_revision = -1
var last_count = -1
var person_routes: Array = []
var last_worker_signature = ""

func setup(simulation) -> void:
	sim = simulation
	material = StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.vertex_color_is_srgb = true
	material.roughness = 0.92
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	fog_material = StandardMaterial3D.new()
	fog_material.vertex_color_use_as_albedo = true
	fog_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	fog_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	selection = MeshInstance3D.new()
	selection.mesh = Art.outline()
	var highlight = StandardMaterial3D.new()
	highlight.vertex_color_use_as_albedo = true
	highlight.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	highlight.cull_mode = BaseMaterial3D.CULL_DISABLED
	selection.material_override = highlight
	selection.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(selection)
	refresh()

func instance(mesh: Mesh, parent: Node, mat: Material = null) -> MeshInstance3D:
	var node = MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material if mat==null else mat
	parent.add_child(node)
	return node

func point(id: String) -> Vector3:
	var p = Art.position(Rules.coord(id))
	p.y = Art.surface(sim.tiles[id]) if sim.tiles.has(id) else 0.8
	return p

func refresh() -> void:
	if last_revision==sim.revision: return
	last_revision = sim.revision
	for id in sim.tiles:
		var t = sim.tiles[id]
		var road_neighbours = ""
		for other in Rules.neighbours(id):
			if sim.tiles.has(other) and (sim.tiles[other].road or sim.tiles[other].building!=""): road_neighbours += other
		var signature = str([t.terrain,t.height,Art.surface(t),t.claimed,t.road,t.building,mini(8,t.trees),int(float(t.ore)/5),int(float(t.deep)/10),int(float(t.stone)/8),t.residents,t.tower_level,t.sapling>=0,int(t.sapling/20),road_neighbours])
		if signatures.get(id)==signature: continue
		signatures[id] = signature
		if tile_nodes.has(id):
			remove_child(tile_nodes[id])
			tile_nodes[id].queue_free()
		var root = Node3D.new()
		root.name = "Plot_"+id.replace(",","_").replace("-","n")
		root.position = Art.position(Vector2i(t.q,t.r))
		add_child(root)
		tile_nodes[id] = root
		var ground = Art.ground(t)
		instance(ground,root)
		var details: Mesh
		if t.terrain!="water":
			details = Art.details(t,sim)
			instance(details,root)
		var body = StaticBody3D.new()
		body.set_meta("tile",id)
		root.add_child(body)
		var collision = CollisionShape3D.new()
		collision.shape = ground.create_trimesh_shape()
		body.add_child(collision)
		# Clicking a tall roof must select its plot, not the ground behind it.
		if t.building!="" and details!=null and details.get_surface_count()>0:
			var structure = CollisionShape3D.new()
			structure.shape = details.create_trimesh_shape()
			body.add_child(structure)
	if last_count!=sim.tiles.size():
		last_count = sim.tiles.size()
		refresh_fog()
	refresh_people()
	select(selected)

func refresh_fog() -> void:
	var border = {}
	for id in sim.tiles:
		for other in Rules.neighbours(id):
			if not sim.tiles.has(other): border[other] = 1
	var outer = border.keys()
	for id in outer:
		for other in Rules.neighbours(id):
			if not sim.tiles.has(other) and not border.has(other): border[other] = 2
	for id in fog_nodes.keys():
		if not border.has(id):
			fog_nodes[id].queue_free()
			fog_nodes.erase(id)
	for id in border:
		if fog_nodes.has(id): continue
		var a = Art.new()
		var tint = Color("a7b9ac") if border[id]==1 else Color("b4c1b4")
		a.cylinder(Vector3(0,0.26,0),Art.RADIUS*0.985,0.22,tint,6)
		var cloud = instance(a.mesh(),self,fog_material)
		cloud.position = Art.position(Rules.coord(id))
		cloud.rotation.y = PI/6
		cloud.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		fog_nodes[id] = cloud

func refresh_people() -> void:
	var homes = []
	for i in range(4): homes.append("0,0")
	var jobs = []
	for id in sim.tiles:
		for i in range(int(sim.tiles[id].residents)): homes.append(id)
		for i in range(int(sim.tiles[id].workers)): jobs.append(id)
	var signature = str(homes)+str(jobs)+str(sim.routes)
	if last_worker_signature==signature: return
	last_worker_signature = signature
	person_routes.clear()
	for i in range(homes.size()):
		var route = sim.path(homes[i])
		if i<jobs.size() and not route.is_empty():
			var work_route = sim.path(jobs[i])
			if not work_route.is_empty():
				work_route.reverse()
				route.append_array(work_route.slice(1))
		person_routes.append({"home":homes[i],"path":route})
	while people.size()<homes.size(): people.append(instance(Art.person(people.size()),self))
	for i in range(people.size()): people[i].visible = i<homes.size()

func select(id: String) -> void:
	selected = id
	selection.visible = sim.tiles.has(id)
	if selection.visible: selection.position = point(id)+Vector3.UP*0.08

func animate() -> void:
	var time = float(sim.state.clock)+float(sim.state.remainder)
	for i in range(person_routes.size()):
		var person = people[i]
		var route = person_routes[i].path
		if route.size()>1:
			var phase = pingpong(time*0.19+i*0.53,float(route.size()-1))
			var edge = mini(int(phase),route.size()-2)
			var a = point(route[edge])
			var b = point(route[edge+1])
			var heading = (b-a).normalized()
			person.position = a.lerp(b,phase-edge)+Vector3(-heading.z,0,heading.x)*0.22
			if fmod(time*0.19+i*0.53,2.0*(route.size()-1))>=route.size()-1: heading = -heading
			person.rotation.y = atan2(heading.x,heading.z)
		else:
			var angle = i*2.4+time*0.08
			person.position = point(person_routes[i].home)+Vector3(cos(angle),0,sin(angle))*1.03
		person.position.y += 0.07+sin(time*5+i)*0.012
	while carts.size()<sim.state.shipments.size():
		carts.append(instance(Art.cart("timber"),self))
	for i in range(carts.size()):
		carts[i].visible = i<sim.state.shipments.size()
		if not carts[i].visible: continue
		var cargo = sim.state.shipments[i]
		if carts[i].get_meta("resource","")!=cargo.resource:
			if not cart_materials.has(cargo.resource): cart_materials[cargo.resource] = Art.cart(cargo.resource)
			carts[i].mesh = cart_materials[cargo.resource]
			carts[i].set_meta("resource",cargo.resource)
		var phase = clampf(float(cargo.elapsed)/cargo.duration,0,0.99999)*(cargo.path.size()-1)
		var edge = mini(int(phase),cargo.path.size()-2)
		var a = point(cargo.path[edge])
		var b = point(cargo.path[edge+1])
		carts[i].position = a.lerp(b,phase-edge)+Vector3.UP*0.08
		carts[i].rotation.y = atan2((b-a).x,(b-a).z)
