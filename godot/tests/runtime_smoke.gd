extends Node

const Scenario = preload("res://tests/frontier_scenario.gd")
const Sim = preload("res://core/simulation.gd")
var failures = 0
var passed = 0
# Exported resource packs are read-only. An explicit output folder lets the
# same checks exercise a packaged build without writing beside the game.
var output_dir = OS.get_environment("ALPHA_HEX_QA_OUTPUT") if OS.has_environment("ALPHA_HEX_QA_OUTPUT") else ProjectSettings.globalize_path("res://qa-output")

func check(condition: bool, message: String) -> void:
	if condition: passed += 1; print("RUNTIME PASS: ",message)
	else: failures += 1; push_error("RUNTIME FAIL: "+message)

func frames(count: int=3) -> void:
	for i in range(count): await get_tree().process_frame

func capture(filename: String) -> void:
	if DisplayServer.get_name()=="headless": return
	await get_tree().create_timer(0.25).timeout
	await RenderingServer.frame_post_draw
	var picture = get_viewport().get_texture().get_image()
	check(picture.save_png(output_dir.path_join(filename))==OK,"Screenshot "+filename)

func key(code: Key) -> void:
	var event = InputEventKey.new()
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	Input.flush_buffered_events()
	await frames(2)
	event = InputEventKey.new()
	event.physical_keycode = code
	event.pressed = false
	Input.parse_input_event(event)
	Input.flush_buffered_events()
	await frames(2)

func click_button(game, id: String) -> void:
	await frames(2)
	var b = game.hud.buttons.get(id)
	check(is_instance_valid(b) and b.is_visible_in_tree() and not b.disabled,"Clickable UI control: "+id)
	if not is_instance_valid(b): return
	var rect = b.get_global_rect()
	check(get_viewport().get_visible_rect().encloses(rect),"UI control is inside window: "+id)
	# Input.parse_input_event receives window pixels; controls use logical units.
	var pointer_position = get_viewport().get_final_transform()*rect.get_center()
	var pointer = InputEventMouseMotion.new()
	pointer.position = pointer_position
	Input.parse_input_event(pointer)
	Input.flush_buffered_events()
	for pressed in [true,false]:
		var event = InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.position = pointer_position
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await frames(2)

func run(game) -> void:
	game.paused = true
	DirAccess.make_dir_recursive_absolute(output_dir)
	var marker = FileAccess.open(output_dir.path_join(".gdignore"),FileAccess.WRITE)
	if marker: marker.close()
	await frames(8)
	check(game.world.tile_nodes.size()==37,"Only revealed terrain is rendered")
	check(game.world.people.size()==4,"Four pioneer figures")
	await capture("01-start.png")
	await key(KEY_B)
	check(game.hud.panel=="build","Keyboard opens construction")
	await click_button(game,"build_house")
	game.select_tile("0,1")
	await key(KEY_ENTER)
	check(game.sim.tiles["0,1"].building=="house" and game.sim.tiles["0,1"].residents==0,"Build command through HUD creates an empty cottage")
	await key(KEY_T)
	check(game.hud.panel=="town","Keyboard opens workforce")
	await key(KEY_ESCAPE)
	var scenario = Scenario.new().run()
	check(scenario.ok,"Earned-resource frontier scenario: "+str(scenario.get("error","complete")))
	if not scenario.ok: get_tree().quit(1); return
	for item in [["village","02-village.png","-1,0"],["discovery","03-discovery.png","0,5"],["extraction","04-quarry-delivery.png","0,5"],["expansion","05-expansion.png","0,2"]]:
		var restored = Sim.new()
		check(restored.restore(scenario.snapshots[item[0]])=="","Restore "+item[0]+" checkpoint")
		game.replace_sim(restored)
		game.select_tile(item[2])
		if item[0] in ["discovery","extraction","expansion"]:
			game.focus_tile("0,2")
			game.zoom = 41
		game.world.animate()
		await frames(5)
		await capture(item[1])
	game.hud.toggle_panel("town")
	await frames()
	var count = game.sim.tiles["0,5"].workers
	await click_button(game,"crew_plus_0,5")
	check(game.sim.tiles["0,5"].workers==count+1,"Workforce UI changes real quarry staffing")
	await key(KEY_SPACE)
	check(not game.paused,"Space resumes the game after clicking a crew button")
	await key(KEY_SPACE)
	check(game.paused and game.sim.tiles["0,5"].workers==count+1,"Space pauses without activating the previously clicked control")
	await frames()
	await capture("06-workforce.png")
	game.hud.toggle_panel("research")
	await frames()
	await capture("07-research.png")
	game.hud.toggle_panel("research")
	game.select_tile("0,0")
	game.home()
	await frames()
	# The picking ray is tested through the same input path as a world click.
	var pick = game.camera.unproject_position(game.world.point("1,0"))
	game.pending_pick = pick
	await get_tree().physics_frame
	await get_tree().physics_frame
	await frames()
	check(game.selected=="1,0","Camera ray selects the matching hex collider (selected "+game.selected+")")
	game.pending_pick = game.camera.unproject_position(game.world.point("0,2")+Vector3(0,4.0,0))
	await get_tree().physics_frame
	await get_tree().physics_frame
	await frames()
	check(game.selected=="0,2","Clicking a tall watchtower roof selects its own plot")
	# Synthesized touch gestures check the handler math, not physical-device usability.
	for item in [[0,Vector2(500,400)],[1,Vector2(600,400)]]:
		var touch = InputEventScreenTouch.new()
		touch.index = item[0]; touch.position = item[1]; touch.pressed = true
		game._unhandled_input(touch)
	var old_zoom = game.zoom
	var drag = InputEventScreenDrag.new()
	drag.index = 1; drag.position = Vector2(640,430); drag.relative = Vector2(40,30)
	game._unhandled_input(drag)
	check(game.zoom<old_zoom,"Two-finger spread zooms the camera")
	for index in [0,1]:
		var touch = InputEventScreenTouch.new()
		touch.index = index; touch.pressed = false
		game._unhandled_input(touch)
	check(game.pending_pick.x<0 and game.touches.is_empty(),"Ending a touch gesture does not select a plot")
	game.home()
	if DisplayServer.get_name()!="headless":
		get_window().size = Vector2i(1366,768)
		await frames(5)
		await capture("08-desktop-1366.png")
		get_window().size = Vector2i(1024,768)
		await frames(5)
		game.hud.toggle_panel("build")
		await frames()
		await capture("09-compact-ui.png")
		await click_button(game,"build_forager")
		check(game.hud.active_build=="forager","Compact UI click selects the intended building")
		check(get_viewport().get_visible_rect().encloses(game.hud.inspector.get_global_rect()) and get_viewport().get_visible_rect().encloses(game.hud.dock.get_global_rect()),"Inspector and dock fit the compact window")
	await key(KEY_M)
	check(game.hud.panel=="menu","Keyboard opens the settlement menu")
	await capture("10-save-exit-menu.png")
	await click_button(game,"save_exit")
	check(game.hud.toast_label.text=="Test session: your settlement saves are untouched.","Save & Exit respects QA save protection and keeps the session open")
	print("Runtime checks: %d passed, %d failed; frontier %.1f simulated minutes, %d commands" % [passed,failures,scenario.seconds/60.0,scenario.commands])
	get_tree().quit(0 if failures==0 else 1)
