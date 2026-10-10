class_name FrontierArt
extends RefCounted

const Rules = preload("res://core/rules.gd")
const RADIUS = 2.35
const WOOD = Color("75513a")
const TIMBER = Color("b18651")
const PLASTER = Color("e5d9b4")
const ROOF = Color("a05e43")
const SLATE = Color("4f6967")
const GOLD = Color("d9b562")
const ROCK = Color("9aa7a0")
const LEAF = Color("547b54")
var vertices = PackedVector3Array()
var normals = PackedVector3Array()
var colors = PackedColorArray()

static func position(c: Vector2i) -> Vector3:
	return Vector3(sqrt(3.0)*RADIUS*(c.x+c.y*0.5),0,1.5*RADIUS*c.y)

static func surface(t: Dictionary) -> float:
	var depth = 0.0
	if t.initial_ore>0: depth += floorf(3.0*(1.0-float(t.ore)/t.initial_ore))*0.14
	if t.initial_deep>0: depth += floorf(3.0*(1.0-float(t.deep)/t.initial_deep))*0.10
	if t.initial_stone>0: depth += floorf(3.0*(1.0-float(t.stone)/t.initial_stone))*0.12
	return 0.8 + float(t.height)*0.24-depth

func triangle(a: Vector3,b: Vector3,c: Vector3,color: Color) -> void:
	var normal = (b-a).cross(c-a).normalized()
	# Godot front faces are clockwise; the supplied normal remains outward.
	for p in [a,c,b]:
		vertices.append(p)
		normals.append(normal)
		colors.append(color)

func quad(a: Vector3,b: Vector3,c: Vector3,d: Vector3,color: Color) -> void:
	triangle(a,b,c,color)
	triangle(a,c,d,color)

func box(center: Vector3,size: Vector3,color: Color,angle: float=0.0) -> void:
	var points = []
	var basis = Basis(Vector3.UP,angle)
	for offset in [Vector3(-1,-1,-1),Vector3(1,-1,-1),Vector3(1,1,-1),Vector3(-1,1,-1),Vector3(-1,-1,1),Vector3(1,-1,1),Vector3(1,1,1),Vector3(-1,1,1)]:
		points.append(center+basis*(offset*size*0.5))
	for f in [[0,3,2,1],[4,5,6,7],[0,4,7,3],[1,2,6,5],[3,7,6,2],[0,1,5,4]]:
		quad(points[f[0]],points[f[1]],points[f[2]],points[f[3]],color)

func cylinder(center: Vector3,radius: float,height: float,color: Color,sides: int=8,top_radius: float=-1.0) -> void:
	if top_radius<0: top_radius = radius
	for i in range(sides):
		var a = TAU*i/sides
		var b = TAU*(i+1)/sides
		var bottom_a = center+Vector3(cos(a)*radius,-height*0.5,sin(a)*radius)
		var bottom_b = center+Vector3(cos(b)*radius,-height*0.5,sin(b)*radius)
		var top_a = center+Vector3(cos(a)*top_radius,height*0.5,sin(a)*top_radius)
		var top_b = center+Vector3(cos(b)*top_radius,height*0.5,sin(b)*top_radius)
		quad(bottom_a,top_a,top_b,bottom_b,color.darkened(0.025*(i%3)))
		triangle(center+Vector3.UP*height*0.5,top_b,top_a,color.lightened(0.035))

func roof(center: Vector3,width: float,height: float,length: float,color: Color) -> void:
	var a = center+Vector3(-width/2,0,-length/2)
	var b = center+Vector3(width/2,0,-length/2)
	var c = center+Vector3(0,height,-length/2)
	var d = center+Vector3(-width/2,0,length/2)
	var e = center+Vector3(width/2,0,length/2)
	var f = center+Vector3(0,height,length/2)
	triangle(a,c,b,PLASTER)
	triangle(d,e,f,PLASTER)
	quad(a,d,f,c,color)
	quad(b,c,f,e,color.darkened(0.14))
	box(center+Vector3(0,height,0),Vector3(0.065,0.065,length+0.07),WOOD)

func mesh() -> ArrayMesh:
	var out = ArrayMesh.new()
	if vertices.is_empty(): return out
	var arrays = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	return out

static func ground(t: Dictionary) -> ArrayMesh:
	var art = FrontierArt.new()
	var y = surface(t)
	var tint = {"grass":Color("8b9f69"),"forest":Color("789064"),"ore":Color("93937c"),"rock":Color("9a9a83"),"stone":Color("a9ad94"),"sand":Color("c8bd8c"),"water":Color("7daba6")}.get(t.terrain,Color("8b9f69"))
	tint = tint.lightened(0.035*((int(t.q)*3+int(t.r)*7+100)%4))
	if not t.claimed: tint = tint.darkened(0.08)
	for i in range(6):
		var a = deg_to_rad(30.0+i*60.0)
		var b = deg_to_rad(30.0+(i+1)*60.0)
		var p = Vector3(cos(a),0,sin(a))*RADIUS*0.975
		var q = Vector3(cos(b),0,sin(b))*RADIUS*0.975
		art.triangle(Vector3(0,y,0),q+Vector3.UP*y,p+Vector3.UP*y,tint.lightened(0.015*(i%2)))
		art.quad(p+Vector3.UP*y,q+Vector3.UP*y,q,p,Color("777862").darkened(0.025*(i%3)))
		art.quad(p+Vector3.UP*(y-0.075),q+Vector3.UP*(y-0.075),q+Vector3.UP*y,p+Vector3.UP*y,tint.darkened(0.12))
	return art.mesh()

func tree(p: Vector3,size: float,variant: int=0) -> void:
	cylinder(p+Vector3.UP*size*0.48,size*0.075,size,WOOD,6)
	if variant%3==0:
		for i in range(3): cylinder(p+Vector3.UP*size*(0.8+i*0.29),size*(0.48-i*0.09),size*0.7,LEAF.darkened(i*0.035),7,0.02)
	else:
		cylinder(p+Vector3.UP*size*1.05,size*0.46,size*0.58,LEAF.lightened(0.05*(variant%3)),7,size*0.31)
		cylinder(p+Vector3.UP*size*1.44,size*0.32,size*0.37,LEAF.lightened(0.06),7,0.08)

func cottage(p: Vector3,size: float=1.0,color: Color=ROOF,occupied: bool=true) -> void:
	box(p+Vector3(0,0.12,0)*size,Vector3(1.42,0.24,1.32)*size,ROCK)
	box(p+Vector3(0,0.72,0)*size,Vector3(1.24,1.04,1.14)*size,PLASTER)
	for x in [-0.61,0.0,0.61]:
		for z in [-0.58,0.58]: box(p+Vector3(x,0.72,z)*size,Vector3(0.07,1.1,0.055)*size,WOOD)
	for y in [0.25,1.08]: box(p+Vector3(0,y,0.591)*size,Vector3(1.28,0.07,0.06)*size,WOOD)
	roof(p+Vector3.UP*1.24*size,1.56*size,0.57*size,1.50*size,color)
	box(p+Vector3(-0.21,0.5,0.595)*size,Vector3(0.3,0.68,0.035)*size,WOOD.darkened(0.18))
	box(p+Vector3(0.37,0.81,0.597)*size,Vector3(0.27,0.30,0.035)*size,GOLD if occupied else SLATE)
	box(p+Vector3(0.37,0.81,0.62)*size,Vector3(0.022,0.32,0.025)*size,WOOD)
	box(p+Vector3(0.37,0.81,0.62)*size,Vector3(0.29,0.023,0.025)*size,WOOD)
	box(p+Vector3(0.37,1.52,-0.3)*size,Vector3(0.22,0.9,0.24)*size,ROCK.darkened(0.13))

func fence(p: Vector3,length: float,angle: float=0.0) -> void:
	var basis = Basis(Vector3.UP,angle)
	for x in [-length/2,0,length/2]: box(p+basis*Vector3(x,0.22,0),Vector3(0.07,0.44,0.07),TIMBER,angle)
	for h in [0.15,0.33]: box(p+Vector3.UP*h,Vector3(length,0.055,0.04),TIMBER,angle)

static func details(t: Dictionary,sim) -> ArrayMesh:
	var a = FrontierArt.new()
	var base = Vector3(0,surface(t),0)
	var rng = RandomNumberGenerator.new()
	rng.seed = int(t.q)*100071+int(t.r)*76847+219
	var occupied = t.building!=""
	for i in range(mini(8,int(t.trees))):
		var angle = i*2.39996+rng.randf()*0.3
		var radius = 1.72+rng.randf()*0.18 if occupied else 0.65+(i%3)*0.45
		a.tree(base+Vector3(cos(angle)*radius,0,sin(angle)*radius),0.68+rng.randf()*0.40,i)
	if t.sapling>=0:
		var scale = 0.22+float(t.sapling)/90.0*0.5
		a.tree(base+Vector3(-1.3,0,0.5),scale)
	var resource_left = t.ore if t.initial_ore>0 else t.stone
	if resource_left>0:
		var initial = t.initial_ore if t.initial_ore>0 else t.initial_stone
		for i in range(ceili(6.0*resource_left/initial)):
			var p = base+Vector3(rng.randf_range(-1.5,1.5),0.12,rng.randf_range(-1.45,1.45))
			if occupied: p.x = (-1 if i%2==0 else 1)*1.6
			a.cylinder(p,0.25+rng.randf()*0.34,0.3+rng.randf()*0.35,ROCK if t.initial_stone>0 else Color("72767a"),5,0.16)
			if t.initial_ore>0: a.box(p+Vector3(0.05,0.27,0),Vector3(0.16,0.05,0.15),Color("b78857"),rng.randf()*TAU)
	# Small ground details fill the larger plots without increasing building bulk.
	if t.terrain in ["grass","forest"]:
		for i in range(9):
			var p = base+Vector3(rng.randf_range(-1.85,1.85),0.018,rng.randf_range(-1.5,1.5))
			if p.x*p.x+p.z*p.z>4.0: continue
			a.box(p,Vector3(0.10,0.055,0.075),Color("adbb7b"),rng.randf()*TAU)
	match t.building:
		"depot":
			a.cottage(base,1.05,SLATE)
			a.box(base+Vector3(-1.03,0.31,0.25),Vector3(0.48,0.62,0.52),TIMBER)
			a.cylinder(base+Vector3(0.97,0.31,0.45),0.23,0.62,WOOD,10)
			a.box(base+Vector3(0.94,1.5,-0.2),Vector3(0.06,3.0,0.06),WOOD)
			a.box(base+Vector3(1.24,2.70,-0.2),Vector3(0.6,0.40,0.035),GOLD)
		"house":
			a.cottage(base+Vector3(0,0,-0.2),0.90,ROOF,int(t.residents)>0)
			a.fence(base+Vector3(0,0,1.38),2.2)
			a.fence(base+Vector3(-1.23,0,0.8),1.1,PI/2)
			for row in range(3):
				a.box(base+Vector3(0.68,0.018,0.72+row*0.18),Vector3(0.6,0.035,0.10),WOOD)
				for i in range(4): a.box(base+Vector3(0.44+i*0.14,0.075,0.72+row*0.18),Vector3(0.09,0.11,0.09),LEAF)
		"forager":
			a.cottage(base+Vector3(0,0,-0.3),0.7,SLATE)
			a.box(base+Vector3(-0.45,0.4,0.85),Vector3(1.1,0.12,0.55),TIMBER)
			for x in [-0.77,-0.18,0.55]: a.cylinder(base+Vector3(x,0.6 if x<0 else 0.16,0.85),0.17,0.25,GOLD,8)
		"logging","forester":
			a.cottage(base+Vector3(0,0,-0.4),0.67,SLATE if t.building=="forester" else ROOF)
			for i in range(5):
				if t.building=="logging": a.box(base+Vector3(-0.45+i*0.20,0.18,0.86),Vector3(0.15,0.25,0.96),TIMBER)
				else: a.tree(base+Vector3(-0.6+i*0.3,0,0.87),0.26,i)
		"farm":
			a.cottage(base+Vector3(0,0,-0.87),0.63,ROOF)
			for row in range(5):
				a.box(base+Vector3(0,0.025,0.08+row*0.27),Vector3(2.1,0.05,0.18),WOOD.darkened(0.17))
				for i in range(9): a.box(base+Vector3(-0.92+i*0.23,0.19,0.08+row*0.27),Vector3(0.09,0.33,0.08),GOLD if row%2==0 else LEAF)
			a.fence(base+Vector3(-1.28,0,0.4),1.9,PI/2)
		"mine","deep_mine","quarry":
			a.box(base+Vector3(0,0.025,0.12),Vector3(1.75,0.05,1.5),Color("545d53"))
			for i in range(3): a.box(base+Vector3(0,0.08*i,-0.49+i*0.27),Vector3(1.5,0.16,0.22),ROCK.darkened(i*0.10))
			for x in [-0.56,0.56]: a.box(base+Vector3(x,0.68,0.3),Vector3(0.14,1.36,0.16),WOOD)
			a.box(base+Vector3(0,1.38,0.3),Vector3(1.45,0.16,0.20),TIMBER)
			a.roof(base+Vector3(0,1.47,0.15),1.65,0.35,0.9,SLATE)
			if t.building=="deep_mine":
				a.box(base+Vector3(0.8,1.25,-0.7),Vector3(0.18,2.5,0.18),WOOD)
				a.box(base+Vector3(0.2,2.35,-0.7),Vector3(1.5,0.16,0.2),WOOD)
			if t.building=="quarry":
				for i in range(4): a.box(base+Vector3(1.2,0.16+i*0.16,0.6),Vector3(0.52,0.14,0.5),ROCK)
		"smelter":
			a.cottage(base+Vector3(-0.3,0,-0.24),0.8,SLATE)
			a.cylinder(base+Vector3(0.77,0.62,0.2),0.38,1.24,ROCK.darkened(0.14),8,0.27)
			a.cylinder(base+Vector3(0.77,1.68,0.2),0.16,1.15,ROCK.darkened(0.30),6)
			a.box(base+Vector3(0.77,0.29,0.565),Vector3(0.26,0.35,0.035),Color("de9760"))
			for i in range(3): a.box(base+Vector3(-0.48+i*0.25,0.16,1.08),Vector3(0.2,0.2,0.47),Color("687e83"))
		"watchtower":
			var stone = int(t.tower_level)==2
			if stone:
				for level in range(3): a.box(base+Vector3(0,0.3+level*0.45,0),Vector3(1.25-level*0.10,0.44,1.25-level*0.10),ROCK.darkened(level*0.03))
			for x in [-0.48,0.48]:
				for z in [-0.48,0.48]: a.box(base+Vector3(x,1.60,z),Vector3(0.14,3.2,0.14),WOOD)
			a.box(base+Vector3(0,2.61,0),Vector3(1.60,0.14,1.60),TIMBER)
			a.roof(base+Vector3(0,3.62,0),1.9,0.6,1.9,SLATE if stone else ROOF)
			for x in [-0.71,0.71]: a.box(base+Vector3(x,2.94,0),Vector3(0.10,0.55,1.5),TIMBER)
			for z in [-0.71,0.71]: a.box(base+Vector3(0,2.94,z),Vector3(1.5,0.55,0.1),TIMBER)
			for i in range(10): a.box(base+Vector3(0,0.14+i*0.25,0.56),Vector3(0.56,0.065,0.12),TIMBER)
			a.box(base+Vector3(0.1,4.38,0),Vector3(0.05,1.0,0.05),WOOD)
			a.box(base+Vector3(0.36,4.62,0),Vector3(0.53,0.28,0.035),GOLD)
	if t.road or t.building!="":
		var center = Vector3(0,surface(t)+0.032,0)
		a.cylinder(center,0.46,0.03,Color("c0b08a"),10)
		var c = Vector2i(t.q,t.r)
		for delta in Rules.DIRECTIONS:
			var other = Rules.key(c+delta)
			if not sim.tiles.has(other) or not sim.tiles[other].claimed: continue
			var neighbour = sim.tiles[other]
			if not neighbour.road and neighbour.building=="": continue
			var end = position(delta)*0.5
			end.y = (surface(t)+surface(neighbour))*0.5+0.045
			var side = Vector3(-end.z,0,end.x).normalized()*0.22
			a.quad(center-side,center+side,end+side,end-side,Color("c0b08a"))
	return a.mesh()

static func person(index: int) -> ArrayMesh:
	var a = FrontierArt.new()
	for x in [-0.055,0.055]: a.box(Vector3(x,0.09,0),Vector3(0.075,0.18,0.10),WOOD.darkened(0.15))
	a.box(Vector3(0,0.29,0),Vector3(0.22,0.26,0.14),[Color("bb8153"),SLATE,LEAF][index%3])
	a.cylinder(Vector3(0,0.49,0),0.083,0.13,Color("cfaa82"),7)
	a.cylinder(Vector3(0,0.565,0),0.13,0.038,PLASTER,8)
	a.cylinder(Vector3(0,0.60,0),0.085,0.065,PLASTER,8)
	return a.mesh()

static func cart(resource: String) -> ArrayMesh:
	var a = FrontierArt.new()
	a.box(Vector3(0,0.25,0),Vector3(0.50,0.17,0.68),TIMBER)
	for x in [-0.29,0.29]:
		for z in [-0.2,0.2]: a.box(Vector3(x,0.15,z),Vector3(0.065,0.29,0.25),WOOD)
	var color = {"timber":TIMBER,"ore":SLATE,"metal":Color("a0b6b9"),"food":GOLD,"stone":ROCK}.get(resource,ROCK)
	a.box(Vector3(0,0.46,0),Vector3(0.41,0.25,0.52),color)
	return a.mesh()

static func outline() -> ArrayMesh:
	var a = FrontierArt.new()
	for i in range(6):
		var x = deg_to_rad(30.0+i*60.0)
		var y = deg_to_rad(30.0+(i+1)*60.0)
		var p = Vector3(cos(x),0,sin(x))
		var q = Vector3(cos(y),0,sin(y))
		a.quad(p*RADIUS*0.97,q*RADIUS*0.97,q*RADIUS*0.935,p*RADIUS*0.935,GOLD)
	return a.mesh()
