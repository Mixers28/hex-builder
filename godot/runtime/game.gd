extends Node3D

const Sim = preload("res://core/simulation.gd")
const Rules = preload("res://core/rules.gd")
const Store = preload("res://core/save_store.gd")
const World = preload("res://runtime/world_view.gd")
const Hud = preload("res://runtime/game_hud.gd")
const Art = preload("res://runtime/world_art.gd")
var sim
var world
var hud
var camera: Camera3D
var focus = Vector3.ZERO
var yaw = deg_to_rad(32.0)
var zoom = 30.0
var paused = false
var speed = 1
var tool = "inspect"
var selected = ""
var pending_pick = Vector2(-1,-1)
var drag_mode = 0
var hud_timer = 0.0
var save_timer = 0.0
var can_autosave = true
var qa = false
var touches = {}
var touch_moved = {}
var import_picker: FileDialog

func _ready() -> void:
	get_tree().auto_accept_quit = false
	qa = "--qa" in OS.get_cmdline_user_args()
	sim = Sim.new()
	sim.start()
	var startup_message = ""
	if not qa and (FileAccess.file_exists(Store.PRIMARY) or FileAccess.file_exists(Store.PRIMARY+".bak")):
		var loaded = Store.load_with_backup()
		if loaded.ok:
			sim = loaded.simulation
			if loaded.get("recovered",false): startup_message = "Recovered your settlement from its backup."
		else:
			can_autosave = false
			startup_message = "Save could not be loaded. Autosave is paused to preserve it. "+loaded.error
	setup_environment()
	world = World.new()
	add_child(world)
	world.setup(sim)
	hud = Hud.new()
	add_child(hud)
	hud.setup(self)
	select_tile("0,0")
	if startup_message!="": hud.toast(startup_message)
	if qa: call_deferred("run_qa")

func setup_environment() -> void:
	var environment = WorldEnvironment.new()
	var settings = Environment.new()
	settings.background_mode = Environment.BG_COLOR
	settings.background_color = Color("b4c1b4")
	settings.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	settings.ambient_light_color = Color("d9e1cd")
	settings.ambient_light_energy = 0.25
	settings.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	environment.environment = settings
	add_child(environment)
	var sun = DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-52,-32,0)
	sun.light_color = Color("fff0d0")
	sun.light_energy = 0.55
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 140
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	add_child(sun)
	var ground = MeshInstance3D.new()
	var plane = PlaneMesh.new()
	plane.size = Vector2(1000,1000)
	ground.mesh = plane
	ground.position.y = -0.04
	var mat = StandardMaterial3D.new()
	mat.albedo_color = Color("b4c1b4")
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	ground.material_override = mat
	add_child(ground)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.near = 0.1
	camera.far = 350
	add_child(camera)
	camera.current = true
	update_camera()

func _process(delta: float) -> void:
	if sim==null: return
	if not paused: sim.step(minf(delta,0.25)*speed)
	var pan = Vector2.ZERO
	if Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP): pan.y -= 1
	if Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN): pan.y += 1
	if Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT): pan.x -= 1
	if Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT): pan.x += 1
	if Input.is_physical_key_pressed(KEY_Q): yaw -= delta*0.8
	if Input.is_physical_key_pressed(KEY_E): yaw += delta*0.8
	pan_camera(pan*delta*zoom*0.32)
	update_camera()
	world.refresh()
	world.animate()
	hud_timer += delta
	if hud_timer>=0.3:
		hud_timer = 0
		hud.refresh()
	save_timer += delta
	if save_timer>=30:
		save_timer = 0
		if can_autosave and not qa:
			var error = Store.write(sim)
			if error!="": can_autosave = false; hud.toast(error+" Autosave paused.")

func _physics_process(_delta: float) -> void:
	if pending_pick.x<0: return
	var from = camera.project_ray_origin(pending_pick)
	var query = PhysicsRayQueryParameters3D.create(from,from+camera.project_ray_normal(pending_pick)*300)
	var hit = get_world_3d().direct_space_state.intersect_ray(query)
	pending_pick = Vector2(-1,-1)
	if not hit.is_empty() and hit.collider.has_meta("tile"):
		select_tile(hit.collider.get_meta("tile"))
	else: hud.toast("Uncharted land. A connected watchtower opens the horizon.")

func _input(event: InputEvent) -> void:
	# Releasing over a panel must still end a camera drag started in the world.
	if event is InputEventMouseButton and not event.pressed and event.button_index in [MOUSE_BUTTON_MIDDLE,MOUSE_BUTTON_RIGHT]: drag_mode = 0

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index==MOUSE_BUTTON_LEFT and event.pressed: pending_pick = event.position
		elif event.button_index==MOUSE_BUTTON_MIDDLE: drag_mode = 1 if event.pressed else 0
		elif event.button_index==MOUSE_BUTTON_RIGHT: drag_mode = 2 if event.pressed else 0
		elif event.button_index==MOUSE_BUTTON_WHEEL_UP and event.pressed: zoom = maxf(15,zoom*0.9)
		elif event.button_index==MOUSE_BUTTON_WHEEL_DOWN and event.pressed: zoom = minf(65,zoom/0.9)
	elif event is InputEventMouseMotion:
		if drag_mode==1: pan_camera(-event.relative*zoom/900.0)
		elif drag_mode==2: yaw -= event.relative.x*0.007
	elif event is InputEventMagnifyGesture: zoom = clampf(zoom/event.factor,15,65)
	elif event is InputEventPanGesture: pan_camera(event.delta*zoom/80.0)
	elif event is InputEventScreenTouch:
		if event.pressed:
			touches[event.index] = event.position
			touch_moved[event.index] = false
			if touches.size()>1:
				for index in touches: touch_moved[index] = true
		else:
			if touches.has(event.index) and not touch_moved.get(event.index,true): pending_pick = event.position
			touches.erase(event.index)
			touch_moved.erase(event.index)
	elif event is InputEventScreenDrag:
		if not touches.has(event.index): return
		touch_moved[event.index] = true
		if touches.size()==1:
			pan_camera(-event.relative*zoom/900.0)
			touches[event.index] = event.position
		elif touches.size()==2:
			var fingers = touches.keys()
			var before = touches[fingers[1]]-touches[fingers[0]]
			touches[event.index] = event.position
			var after = touches[fingers[1]]-touches[fingers[0]]
			if before.length()>10 and after.length()>10:
				zoom = clampf(zoom*before.length()/after.length(),15,65)
				yaw -= before.angle_to(after)
	elif event is InputEventKey and event.pressed and not event.echo:
		match event.physical_keycode:
			KEY_SPACE: toggle_pause()
			KEY_B: hud.toggle_panel("build")
			KEY_T: hud.toggle_panel("town")
			KEY_R: hud.toggle_panel("research")
			KEY_M: hud.toggle_panel("menu")
			KEY_1: hud.choose_tool("inspect")
			KEY_2: hud.choose_tool("claim")
			KEY_3: hud.choose_tool("road")
			KEY_HOME,KEY_7: home()
			KEY_F: command(sim.try_forage(selected))
			KEY_ENTER: apply_action()
			KEY_F5: save_game()
			KEY_F9: load_game()
			KEY_ESCAPE:
				if hud.panel!="": hud.toggle_panel(hud.panel)
				else: hud.choose_tool("inspect"); select_tile("")

func pan_camera(delta: Vector2) -> void:
	var right = Vector3(cos(yaw),0,-sin(yaw))
	var forward = Vector3(sin(yaw),0,cos(yaw))
	focus += right*delta.x+forward*delta.y
	# Camera bounds follow discoveries, not the hidden world's maximum size.
	var min_x = 0.0
	var max_x = 0.0
	var min_z = 0.0
	var max_z = 0.0
	for id in sim.tiles:
		var p = Art.position(Rules.coord(id))
		min_x = minf(min_x,p.x); max_x = maxf(max_x,p.x)
		min_z = minf(min_z,p.z); max_z = maxf(max_z,p.z)
	focus.x = clampf(focus.x,min_x-3,max_x+3)
	focus.z = clampf(focus.z,min_z-3,max_z+3)

func update_camera() -> void:
	camera.size = zoom
	camera.position = focus+Vector3(sin(yaw)*36,38,cos(yaw)*36)
	camera.look_at(focus)

func select_tile(id: String) -> void:
	selected = id
	world.select(id)
	hud.set_selection(id)

func focus_tile(id: String) -> void:
	if sim.tiles.has(id): focus = Art.position(Rules.coord(id))

func home() -> void:
	focus = Vector3.ZERO
	zoom = 30
	select_tile("0,0")

func command(outcome: Dictionary) -> void:
	hud.toast(outcome.message)
	world.refresh()
	hud.refresh(true)

func apply_action() -> void:
	if selected=="": hud.toast("Select a plot first."); return
	match tool:
		"claim": command(sim.try_claim(selected))
		"road": command(sim.try_road(selected))
		"build": command(sim.try_build(selected,hud.active_build))
		_: hud.toast("Choose a building or action in the site panel.")

func toggle_pause() -> void:
	paused = not paused
	hud.refresh()

func cycle_speed() -> void:
	speed = 2 if speed==1 else 1
	hud.refresh()

func save_game() -> bool:
	if qa: hud.toast("Test session: your settlement saves are untouched."); return false
	var error = Store.write(sim)
	if error=="": can_autosave = true
	hud.toast("Settlement saved." if error=="" else error)
	return error==""

func save_and_exit() -> void:
	# quit() does not send a window-close notification; finish saving first.
	if save_game(): get_tree().quit()

func load_game(path: String = Store.PRIMARY) -> void:
	if qa: return
	var loaded = Store.load_with_backup(path)
	if not loaded.ok: hud.toast(loaded.error); return
	replace_sim(loaded.simulation)
	can_autosave = true
	hud.toast("Settlement restored."+(" Recovered from backup." if loaded.get("recovered",false) else ""))

func replace_sim(next_sim) -> void:
	sim = next_sim
	remove_child(world)
	world.queue_free()
	world = World.new()
	add_child(world)
	world.setup(sim)
	hud.signature = ""
	hud.panel_signature = ""
	hud.active_build = ""
	tool = "inspect"
	hud.toast_time = 0
	home()

func load_previous() -> void:
	load_game("user://previous.json")

func confirm_new() -> void:
	var dialog = ConfirmationDialog.new()
	dialog.title = "Begin a new settlement"
	dialog.dialog_text = "Archive this village in Previous settlement and start with a new frontier?"
	dialog.confirmed.connect(func():
		if qa: dialog.queue_free(); return
		var error = Store.write(sim,"user://previous.json")
		if error!="": hud.toast(error); dialog.queue_free(); return
		var next = Sim.new()
		next.start(int(Time.get_unix_time_from_system())%2147483647)
		error = Store.write(next)
		if error=="": replace_sim(next); can_autosave = true; hud.toast("A new beginning. Your previous village is archived.")
		else: hud.toast(error)
		dialog.queue_free()
	)
	dialog.canceled.connect(dialog.queue_free)
	add_child(dialog)
	dialog.popup_centered(Vector2i(460,170))

func import_dialog() -> void:
	import_picker = FileDialog.new()
	import_picker.title = "Import a Unity settlement"
	import_picker.access = FileDialog.ACCESS_FILESYSTEM
	import_picker.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	import_picker.filters = PackedStringArray(["*.json ; Settlement JSON"])
	import_picker.use_native_dialog = true
	import_picker.file_selected.connect(func(path):
		var loaded = Store.read(path)
		if not loaded.ok: hud.toast(loaded.error)
		elif not qa:
			var error = Store.write(sim,"user://previous.json")
			if error=="": error = Store.write(loaded.simulation)
			if error=="": replace_sim(loaded.simulation); can_autosave = true; hud.toast("Settlement imported. The Unity source file is unchanged.")
			else: hud.toast(error)
		import_picker.queue_free()
	)
	import_picker.canceled.connect(import_picker.queue_free)
	add_child(import_picker)
	import_picker.popup_centered_ratio(0.75)

func _notification(what: int) -> void:
	if what==NOTIFICATION_WM_CLOSE_REQUEST:
		if can_autosave and not qa and sim!=null: save_and_exit()
		else: get_tree().quit()
	elif what==NOTIFICATION_APPLICATION_PAUSED:
		if can_autosave and not qa and sim!=null: Store.write(sim)

func run_qa() -> void:
	var script = load("res://tests/display_checks.gd" if "--qa-display" in OS.get_cmdline_user_args() else "res://tests/runtime_smoke.gd")
	if script==null: return
	var runner = script.new()
	add_child(runner)
	runner.run(self)
