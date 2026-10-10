extends "res://tests/runtime_smoke.gd"

var measurements: Array = []
var prefix = "display-"

func enter_fullscreen() -> bool:
	var window = get_window()
	window.grab_focus()
	await frames(3)
	window.mode = Window.MODE_FULLSCREEN
	# The mode changes before macOS finishes moving the window to its Space.
	# Wait for the actual drawable size, not a fixed animation duration.
	for attempt in range(100):
		await get_tree().create_timer(0.1).timeout
		var screen_size = DisplayServer.screen_get_size(window.current_screen)
		if window.size.x==screen_size.x and window.size.y>=screen_size.y*0.9:
			await frames(10)
			return true
	return false

func sample(game, name: String) -> Dictionary:
	var window = get_window()
	var button = game.hud.buttons.dock_build
	var transform = get_viewport().get_final_transform()
	var pixels = transform * button.get_global_rect()
	var screen = window.current_screen
	# Read the actual framebuffer: ViewportTexture.get_size() can include the
	# canvas stretch a second time, unlike the image returned by get_image().
	var frame = get_viewport().get_texture().get_image()
	var reading = {
		"name": name,
		"window_pixels": [window.size.x,window.size.y],
		"logical_ui": [game.hud.root.size.x,game.hud.root.size.y],
		"screen_pixels": [DisplayServer.screen_get_size(screen).x,DisplayServer.screen_get_size(screen).y],
		"screen_scale": DisplayServer.screen_get_scale(screen),
		"mode": window.mode,
		"button_pixels": [pixels.size.x,pixels.size.y],
		"button_height_fraction": pixels.size.y/window.size.y,
		"canvas_scale": transform.get_scale().x,
		"render_pixels": [frame.get_width(),frame.get_height()]
	}
	measurements.append(reading)
	print("DISPLAY: ",JSON.stringify(reading))
	return reading

func check_layout(game, name: String) -> void:
	var viewport_rect = get_viewport().get_visible_rect()
	for control in [game.hud.top,game.hud.dock,game.hud.left,game.hud.inspector,game.hud.guidance_back]:
		if control.visible:
			check(viewport_rect.encloses(control.get_global_rect()),name+": HUD panel fits the logical viewport")
	check(not game.hud.left.get_global_rect().intersects(game.hud.inspector.get_global_rect()),name+": side panels leave room for the world")
	for id in ["dock_build","dock_town","dock_research","pause","speed","menu"]:
		var button = game.hud.buttons[id]
		check(viewport_rect.encloses(button.get_global_rect()),name+": "+id+" stays on screen")

func check_interaction(game, name: String) -> void:
	if game.hud.panel!="": game.hud.toggle_panel(game.hud.panel)
	await click_button(game,"dock_build")
	check(game.hud.panel=="build",name+": pointer opens Build")
	await click_button(game,"build_house")
	check(game.hud.active_build=="house",name+": pointer chooses a cottage")
	await click_button(game,"dock_town")
	check(game.hud.panel=="town",name+": pointer opens Workers")
	var before = int(game.sim.tiles["-1,1"].workers)
	await click_button(game,"crew_plus_-1,1")
	check(game.sim.tiles["-1,1"].workers==before+1,name+": scaled crew button changes staffing")
	game.command(game.sim.try_workers("-1,1",before))
	await click_button(game,"dock_research")
	check(game.hud.panel=="research",name+": pointer opens Research")
	await key(KEY_ESCAPE)
	game.select_tile("")
	game.hud.choose_tool("inspect")
	await frames()
	# Send window coordinates through the real input conversion, not a direct ray.
	# Aim inside a top-face triangle rather than exactly at the fan's shared vertex.
	var point = game.camera.unproject_position(game.world.point("1,0")+Vector3(0.3,0.01,0.2))
	var raw_point = get_viewport().get_final_transform()*point
	var motion = InputEventMouseMotion.new()
	motion.position = raw_point
	Input.parse_input_event(motion)
	for pressed in [true,false]:
		var event = InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.position = raw_point
		event.pressed = pressed
		Input.parse_input_event(event)
		await frames(3)
	await get_tree().physics_frame
	await get_tree().physics_frame
	await frames(2)
	check(game.selected=="1,0",name+": pointer selects the intended world hex after scaling ("+game.selected+")")
	game.select_tile("0,0")
	game.hud.toggle_panel("build")
	await frames()

func run(game) -> void:
	game.paused = true
	if "--qa-baseline" in OS.get_cmdline_user_args(): prefix = "display-before-"
	DirAccess.make_dir_recursive_absolute(output_dir)
	var marker = FileAccess.open(output_dir.path_join(".gdignore"),FileAccess.WRITE)
	if marker: marker.close()
	if DisplayServer.get_name()=="headless":
		push_error("Display checks require a rendered native window.")
		get_tree().quit(1)
		return
	check(game.sim.try_build("0,1","house").ok,"Display fixture builds a cottage through normal commands")
	check(game.sim.try_build("-1,1","forager").ok,"Display fixture builds a workplace through normal commands")
	game.command(game.sim.try_workers("-1,1",1))
	game.hud.toggle_panel("build")
	await frames(10)
	var window = get_window()
	var original_mode = window.mode
	var original_size = window.size
	window.mode = Window.MODE_WINDOWED
	window.size = Vector2i(1440,900)
	await frames(10)
	sample(game,"window-1440")
	await capture(prefix+"01-window.png")
	check(await enter_fullscreen(),"Fullscreen transition reaches the display's drawable size")
	var fullscreen = sample(game,"native-fullscreen")
	check(window.mode==Window.MODE_FULLSCREEN,"Native fullscreen mode is active")
	check(fullscreen.button_height_fraction>=0.038,"Fullscreen action buttons retain at least 3.8% of screen height")
	check(fullscreen.button_pixels[1]/fullscreen.screen_scale>=32.0,"Fullscreen action buttons are at least 32 display points high")
	check(fullscreen.render_pixels==fullscreen.window_pixels,"3D still renders at the native window resolution")
	check_layout(game,"Fullscreen")
	await capture(prefix+"02-fullscreen.png")
	await check_interaction(game,"Fullscreen")
	window.mode = Window.MODE_WINDOWED
	await get_tree().create_timer(1.0).timeout
	for size in [Vector2i(1366,768),Vector2i(1024,768),Vector2i(1920,1080)]:
		window.size = size
		await frames(10)
		var name = "%dx%d" % [size.x,size.y]
		sample(game,name)
		check_layout(game,name)
		await check_interaction(game,name)
		await capture(prefix+name+".png")
	window.size = original_size
	window.mode = original_mode
	await frames(10)
	var report = FileAccess.open(output_dir.path_join(prefix+"measurements.json"),FileAccess.WRITE)
	if report: report.store_string(JSON.stringify(measurements,"\t")); report.close()
	print("Display checks: %d passed, %d failed" % [passed,failures])
	if failures==0 and "--qa-display-hold" in OS.get_cmdline_user_args():
		await enter_fullscreen()
		print("Display test remains open for manual interaction; settlement saves are disabled.")
		return
	get_tree().quit(0 if failures==0 else 1)
