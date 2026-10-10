class_name FrontierHud
extends CanvasLayer

const Rules = preload("res://core/rules.gd")
const PAPER = Color("f3efe2")
const INK = Color("29473e")
const MUTED = Color("768176")
const LINE = Color("d7dac9")
const GREEN = Color("315f4a")
const GOLD = Color("b68d48")
var game
var root: Control
var theme: Theme
var heading_font: SystemFont
var top: PanelContainer
var left: PanelContainer
var inspector: PanelContainer
var dock: PanelContainer
var left_body: VBoxContainer
var inspector_body: VBoxContainer
var resources = {}
var population: Label
var status: Label
var guidance: Label
var guidance_back: PanelContainer
var clock_label: Label
var toast_label: Label
var toast_panel: PanelContainer
var pause_button: Button
var speed_button: Button
var panel = ""
var active_build = ""
var selected = ""
var toast_time = 0.0
var signature = ""
var panel_signature = ""
var live_labels = {}
var buttons = {}
var confirm_remove = ""
var research_bar: ProgressBar

func style(color: Color, radius: int=12, border: Color=Color.TRANSPARENT) -> StyleBoxFlat:
	var s = StyleBoxFlat.new()
	s.bg_color = color
	s.set_corner_radius_all(radius)
	s.set_border_width_all(1)
	s.border_color = border
	s.content_margin_left = 14
	s.content_margin_right = 14
	s.content_margin_top = 10
	s.content_margin_bottom = 10
	return s

func setup(controller) -> void:
	game = controller
	heading_font = SystemFont.new()
	heading_font.font_names = PackedStringArray(["Georgia","Times New Roman"])
	theme = Theme.new()
	var font = SystemFont.new()
	font.font_names = PackedStringArray(["Helvetica Neue","Arial"])
	font.font_weight = 400
	theme.default_font = font
	theme.default_font_size = 14
	theme.set_color("font_color","Label",INK)
	theme.set_color("font_color","Button",INK)
	theme.set_color("font_hover_color","Button",INK)
	theme.set_color("font_pressed_color","Button",INK)
	theme.set_color("font_disabled_color","Button",MUTED)
	theme.set_stylebox("normal","Button",style(Color("e7e8d9"),8,LINE))
	theme.set_stylebox("hover","Button",style(Color("d9e2cc"),8,GOLD))
	theme.set_stylebox("pressed","Button",style(Color("cbd7bc"),8,GREEN))
	theme.set_stylebox("disabled","Button",style(Color("ebe9de"),8))
	theme.set_stylebox("focus","Button",style(Color(0,0,0,0),8,GOLD))
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.theme = theme
	add_child(root)
	build_top()
	build_dock()
	left = card(root)
	left.position = Vector2(20,137)
	left.size = Vector2(316,570)
	left.visible = false
	left_body = scroll_body(left)
	inspector = card(root)
	inspector.position = Vector2(1106,137)
	inspector.size = Vector2(314,480)
	inspector.visible = false
	inspector_body = scroll_body(inspector)
	toast_panel = card(root)
	toast_panel.position = Vector2(20,737)
	toast_label = label(toast_panel,"",13)
	toast_label.custom_minimum_size.x = 350
	toast_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	toast_panel.visible = false
	guidance_back = PanelContainer.new()
	var note_style = style(Color(0.953,0.937,0.886,0.94),8)
	note_style.content_margin_top = 6
	note_style.content_margin_bottom = 6
	guidance_back.add_theme_stylebox_override("panel",note_style)
	guidance_back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(guidance_back)
	guidance = label(guidance_back,"",13,INK)
	guidance.mouse_filter = Control.MOUSE_FILTER_IGNORE
	clock_label = label(root,"",12,MUTED)
	clock_label.position = Vector2(1170,865)
	clock_label.size.x = 250
	clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	root.resized.connect(resize_layout)
	resize_layout()
	refresh(true)

func card(parent: Node) -> PanelContainer:
	var p = PanelContainer.new()
	var s = style(PAPER,13,Color("e0e2d3"))
	s.shadow_color = Color(0.09,0.18,0.13,0.14)
	s.shadow_size = 10
	s.shadow_offset = Vector2(0,4)
	p.add_theme_stylebox_override("panel",s)
	parent.add_child(p)
	return p

func column(parent: Node, gap: int=10) -> VBoxContainer:
	var c = VBoxContainer.new()
	c.add_theme_constant_override("separation",gap)
	parent.add_child(c)
	return c

func row(parent: Node, gap: int=10) -> HBoxContainer:
	var c = HBoxContainer.new()
	c.add_theme_constant_override("separation",gap)
	parent.add_child(c)
	return c

func label(parent: Node, text: String, size: int=14, color: Color=INK, serif: bool=false) -> Label:
	var l = Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size",size)
	l.add_theme_color_override("font_color",color)
	if serif: l.add_theme_font_override("font",heading_font)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(l)
	return l

func paragraph(parent: Node, text: String, size: int=13, color: Color=MUTED) -> Label:
	var l = label(parent,text,size,color)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return l

func button(parent: Node, text: String, action: Callable, id: String="", primary: bool=false) -> Button:
	var b = Button.new()
	b.text = text
	b.clip_text = text.length()>8
	b.tooltip_text = text
	b.custom_minimum_size.x = 38
	b.custom_minimum_size.y = 38
	b.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	# Gameplay shortcuts keep their meaning after a pointer click on the HUD.
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(action)
	if primary:
		b.add_theme_stylebox_override("normal",style(GREEN,8))
		b.add_theme_color_override("font_color",PAPER)
	parent.add_child(b)
	if id!="": buttons[id] = b
	return b

func line(parent: Node) -> void:
	var l = HSeparator.new()
	var rule = StyleBoxLine.new()
	rule.color = LINE
	rule.thickness = 1
	l.add_theme_stylebox_override("separator",rule)
	l.custom_minimum_size.y = 1
	parent.add_child(l)

func scroll_body(parent: Node) -> VBoxContainer:
	var scroll = ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	parent.add_child(scroll)
	var body = column(scroll,10)
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return body

func build_top() -> void:
	top = card(root)
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top.offset_left = 20
	top.offset_right = -20
	top.offset_top = 18
	top.offset_bottom = 93
	var content = row(top,22)
	var brand = column(content,0)
	brand.custom_minimum_size.x = 194
	label(brand,"ALPHA HEX",12,GREEN)
	label(brand,"First Frontier",26,INK,true)
	for resource in Rules.RESOURCES:
		var stack = column(content,1)
		stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		label(stack,resource.to_upper(),10,MUTED)
		resources[resource] = label(stack,"0",22,INK)
		resources[resource+"_cargo"] = label(stack,"in stores",10,MUTED)
	var controls = row(content,5)
	controls.alignment = BoxContainer.ALIGNMENT_CENTER
	pause_button = button(controls,"Ⅱ",func(): game.toggle_pause(),"pause")
	pause_button.tooltip_text = "Pause / resume [Space]"
	speed_button = button(controls,"1×",func(): game.cycle_speed(),"speed")
	button(controls,"☰",func(): toggle_panel("menu"),"menu").tooltip_text = "Island and saves [M]"
	var town_back = PanelContainer.new()
	var note_style = style(Color(0.953,0.937,0.886,0.92),7)
	note_style.content_margin_top = 4
	note_style.content_margin_bottom = 4
	town_back.add_theme_stylebox_override("panel",note_style)
	town_back.position = Vector2(20,101)
	town_back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(town_back)
	var town_row = row(town_back,16)
	town_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	population = label(town_row,"",13,INK)
	status = label(town_row,"",13,MUTED)

func build_dock() -> void:
	dock = card(root)
	dock.position = Vector2(307,806)
	dock.size = Vector2(826,69)
	var items = row(dock,8)
	for item in [["Inspect","1","inspect"],["Claim","2","claim"],["Road","3","road"],["Build","B","build"],["Workers","T","town"],["Research","R","research"]]:
		var kind = item[2]
		var b = button(items,item[0]+"   "+item[1],func():
			if kind in ["build","town","research"]: toggle_panel(kind)
			else: choose_tool(kind)
		,"dock_"+kind)
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button(items,"⌂",func(): game.home(),"home").tooltip_text = "Return to village [Home]"

func resize_layout() -> void:
	if root==null or left==null: return
	var size = root.size
	left.size = Vector2(316,maxf(330,size.y-330))
	inspector.position = Vector2(size.x-334,137)
	inspector.size = Vector2(314,maxf(330,size.y-330))
	dock.size = Vector2(minf(826,size.x-40),69)
	dock.position = Vector2((size.x-dock.size.x)/2,size.y-94)
	toast_panel.position = Vector2(20,size.y-177)
	toast_panel.size = Vector2(380,0)
	guidance_back.position = Vector2(20,size.y-130)
	guidance_back.size = Vector2(minf(1040,size.x-40),30)
	clock_label.position = Vector2(size.x-274,size.y-21)

func clear(parent: Node) -> void:
	for child in parent.get_children():
		parent.remove_child(child)
		child.queue_free()

func toggle_panel(which: String) -> void:
	panel = "" if panel==which else which
	left.visible = panel!=""
	panel_signature = ""
	refresh(true)

func choose_tool(kind: String) -> void:
	game.tool = kind
	active_build = ""
	panel = ""
	left.visible = false
	signature = ""
	refresh(true)

func choose_build(kind: String) -> void:
	game.tool = "build"
	active_build = kind
	panel = ""
	left.visible = false
	signature = ""
	toast("Choose a claimed plot for "+Rules.title(kind)+", then Build.")
	refresh(true)

func set_selection(id: String) -> void:
	selected = id
	inspector_body.get_parent().scroll_vertical = 0
	confirm_remove = ""
	signature = ""
	refresh(true)

func title_row(parent: Node, title_text: String, close_action: Callable) -> void:
	var r = row(parent)
	var h = label(r,title_text,24,INK,true)
	h.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button(r,"×",close_action)

func rebuild_panel() -> void:
	var scroll = left_body.get_parent()
	var previous_scroll = scroll.scroll_vertical
	clear(left_body)
	live_labels.clear()
	research_bar = null
	if panel=="": return
	var sim = game.sim
	title_row(left_body,{"build":"Build a village","town":"Your people","research":"New possibilities","menu":"Your settlement"}[panel],func(): toggle_panel(panel))
	match panel:
		"build":
			paragraph(left_body,"Choose a purpose for the next plot.")
			for kind in Rules.BUILDINGS:
				var rule = Rules.BUILDINGS[kind]
				var unlocked = rule.research=="" or rule.research in sim.state.research
				var b = button(left_body,rule.name+("  ·  locked" if not unlocked else ""),func(): choose_build(kind),"build_"+kind)
				b.disabled = not unlocked
				paragraph(left_body,Rules.cost_text(rule.cost)+"\n"+rule.detail,12)
		"town":
			live_labels["migration"] = paragraph(left_body,sim.migration_status())
			label(left_body,"WORKFORCE",11,GREEN)
			live_labels["workforce"] = label(left_body,"",17)
			paragraph(left_body,"Assign crews to production. More workers finish jobs faster; each crew still waits for its delivery cart.",12)
			line(left_body)
			var count = 0
			for id in sim.tiles:
				var t = sim.tiles[id]
				var capacity = Rules.BUILDINGS.get(t.building,{}).get("crew",0)
				if capacity==0: continue
				count += 1
				var r = row(left_body,6)
				var name_button = button(r,Rules.title(t.building),func(): game.select_tile(id); game.focus_tile(id))
				name_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				button(r,"−",func(): game.command(game.sim.try_workers(id,int(game.sim.tiles[id].workers)-1)),"crew_minus_"+id)
				live_labels["crew_"+id] = label(r,"%d/%d" % [t.workers,capacity],14)
				button(r,"+",func(): game.command(game.sim.try_workers(id,int(game.sim.tiles[id].workers)+1)),"crew_plus_"+id)
				paragraph(left_body,"Plot "+id+" · "+("connected" if not sim.path(id).is_empty() else "needs a road"),11)
			if count==0: paragraph(left_body,"Build a forager lodge and a woodcutter to put your pioneers to work.")
			line(left_body)
			label(left_body,"HOMES & ARRIVALS",11,GREEN)
			for id in sim.tiles:
				if sim.tiles[id].building=="house":
					live_labels["home_"+id] = label(left_body,"Cottage %s   %d / 4 residents" % [id,sim.tiles[id].residents],13)
		"research":
			paragraph(left_body,"Invest in the capabilities your village needs next. Materials are committed when a project begins.",12)
			if sim.state.research_job!="":
				label(left_body,"IN PROGRESS",11,GREEN)
				label(left_body,Rules.RESEARCH[sim.state.research_job].name,18,INK,true)
				research_bar = progress_bar(left_body)
				live_labels["research_time"] = label(left_body,"",12,MUTED)
				line(left_body)
			for kind in Rules.RESEARCH:
				var rule = Rules.RESEARCH[kind]
				var known = kind in sim.state.research
				label(left_body,rule.name+("  ✓" if known else ""),18,INK,true)
				paragraph(left_body,rule.effect,12)
				if not known:
					paragraph(left_body,"Legacy research credit" if sim.legacy_credit(kind) else Rules.cost_text(rule.cost)+" · %d s" % rule.seconds,11,GOLD)
					var b = button(left_body,"Research",func(): game.command(game.sim.try_research(kind)),"research_"+kind)
					live_labels["research_gate_"+kind] = paragraph(left_body,"",11)
					b.disabled = sim.research_error(kind)!=""
				line(left_body)
		"menu":
			paragraph(left_body,"Your village waits for you. Time advances only while you play.")
			button(left_body,"Save settlement   F5",func(): game.save_game(),"save",true)
			button(left_body,"Save & Exit",func(): game.save_and_exit(),"save_exit").tooltip_text = "Save your settlement and close the game."
			button(left_body,"Load settlement   F9",func(): game.load_game(),"load")
			button(left_body,"Previous settlement",func(): game.load_previous(),"previous")
			button(left_body,"Import Unity save…",func(): game.import_dialog(),"import")
			line(left_body)
			button(left_body,"Begin a new settlement…",func(): game.confirm_new(),"new")
			paragraph(left_body,"A new settlement archives your current town in one Previous settlement slot.",12)
			line(left_body)
			paragraph(left_body,"WASD / drag: pan\nQ / E / right drag: orbit\nWheel / pinch: zoom\nSpace: pause · F: forage\nEnter: apply selected action\nEscape: close panel",12)
	scroll.set_deferred("scroll_vertical",previous_scroll)

func progress_bar(parent: Node) -> ProgressBar:
	var bar = ProgressBar.new()
	bar.custom_minimum_size.y = 6
	bar.show_percentage = false
	bar.add_theme_stylebox_override("background",style(LINE,3))
	bar.add_theme_stylebox_override("fill",style(GREEN,3))
	parent.add_child(bar)
	return bar

func rebuild_inspector() -> void:
	var scroll = inspector_body.get_parent()
	var previous_scroll = scroll.scroll_vertical
	clear(inspector_body)
	var sim = game.sim
	inspector.visible = sim.tiles.has(selected)
	if not inspector.visible: return
	var t = sim.tiles[selected]
	var title_text = Rules.title(t.building) if t.building!="" else {"grass":"Open meadow","forest":"Woodland","ore":"Iron deposit","stone":"Stone outcrop","rock":"Rocky ground","water":"Water","sand":"Sandy ground"}.get(t.terrain,"Open land")
	title_row(inspector_body,title_text,func(): game.select_tile(""))
	label(inspector_body,("YOUR VILLAGE" if t.claimed else "DISCOVERED LAND")+"   /   "+selected,10,GOLD)
	var reserves = PackedStringArray()
	if t.trees>0: reserves.append("%d trees" % t.trees)
	if t.ore>0: reserves.append("%d surface ore" % t.ore)
	if t.deep>0 and t.ore==0: reserves.append("%d deep ore" % t.deep)
	if t.stone>0: reserves.append("%d stone" % t.stone)
	if not reserves.is_empty(): paragraph(inspector_body," · ".join(reserves),13,INK)
	var detail = paragraph(inspector_body,sim.work_status(selected),13)
	detail.name = "WorkStatus"
	if t.building!="" and Rules.BUILDINGS.get(t.building,{}).get("crew",0)>0:
		var r = row(inspector_body)
		button(r,"−",func(): game.command(game.sim.try_workers(selected,int(game.sim.tiles[selected].workers)-1)),"selected_minus")
		var crew = label(r,"%d / %d workers" % [t.workers,Rules.BUILDINGS[t.building].crew],16)
		crew.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		crew.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		button(r,"+",func(): game.command(game.sim.try_workers(selected,int(game.sim.tiles[selected].workers)+1)),"selected_plus")
		var progress = progress_bar(inspector_body)
		progress.name = "JobProgress"
		button(inspector_body,"Resume work" if t.paused else "Pause work & release crew",func(): game.command(game.sim.try_pause_work(selected)))
	if not t.claimed and t.terrain!="water":
		button(inspector_body,"Claim land · 6 timber + 1 metal",func(): game.command(game.sim.try_claim(selected)),"claim",true)
	elif t.claimed:
		if t.building=="":
			if game.tool=="build" and active_build!="":
				button(inspector_body,"Build "+Rules.title(active_build),func(): game.apply_action(),"apply",true)
				paragraph(inspector_body,Rules.cost_text(Rules.BUILDINGS[active_build].cost),12,GOLD)
				var error = sim.build_error(selected,active_build)
				if error!="": paragraph(inspector_body,error,12)
			else:
				button(inspector_body,"Choose a building",func(): toggle_panel("build"),"choose_build",true)
			if not t.road: button(inspector_body,"Lay road · 2 timber",func(): game.command(game.sim.try_road(selected)),"road")
		if t.trees>0 or t.ore>0:
			button(inspector_body,"Gather timber +3" if t.trees>0 else "Gather ore +1",func(): game.command(game.sim.try_gather(selected)),"gather")
		if t.terrain in ["grass","forest"]:
			button(inspector_body,"Forage food +2   F",func(): game.command(game.sim.try_forage(selected)),"forage")
		if t.building=="watchtower" and t.tower_level<2:
			button(inspector_body,"Reinforce watchtower",func(): game.command(game.sim.try_upgrade(selected)),"upgrade",true)
			paragraph(inspector_body,"Masonry · 12 timber · 4 metal · 16 stone\nSurvey radius grows from 4 to 6 hexes.",12)
		if t.building!="depot" and (t.building!="" or t.road):
			button(inspector_body,"Confirm removal" if confirm_remove==removal_token() else "Remove…",func():
				if confirm_remove==removal_token():
					game.command(game.sim.try_remove(selected))
					confirm_remove = ""
				else:
					confirm_remove = removal_token()
					signature = ""
					refresh(true)
			,"remove")
	scroll.set_deferred("scroll_vertical",previous_scroll)

func removal_token() -> String:
	if not game.sim.tiles.has(selected): return ""
	var t = game.sim.tiles[selected]
	return selected+":"+t.building+":"+str(t.road)

func refresh(force: bool=false) -> void:
	if root==null: return
	var sim = game.sim
	for resource in Rules.RESOURCES:
		resources[resource].text = str(sim.state.stock[resource])
		var transit = 0
		for cargo in sim.state.shipments:
			if cargo.resource==resource: transit += int(cargo.amount)
		resources[resource+"_cargo"].text = "+%d on the road" % transit if transit>0 else "in stores"
	population.text = "%d citizens  /  %d places     ·     %d working, %d available" % [sim.state.population,sim.capacity(),sim.assigned(),sim.free_workers()]
	status.text = ("Food shortage · " if sim.state.hungry else "")+"%d food / meal" % ceili(float(sim.state.population)/2.0)
	if not game.can_autosave: status.text = "Saving paused · "+status.text
	status.add_theme_color_override("font_color",Color("a95b47") if sim.state.hungry or not game.can_autosave else MUTED)
	guidance.text = sim.guidance()
	clock_label.text = "Day %d  ·  %d plots discovered" % [1+int(sim.state.clock/600.0),sim.tiles.size()]
	pause_button.text = "▶" if game.paused else "Ⅱ"
	speed_button.text = "%d×" % game.speed
	var next_signature = str([selected,active_build,game.tool,sim.revision,confirm_remove])
	if force or signature!=next_signature:
		signature = next_signature
		rebuild_inspector()
	var next_panel_signature = panel+str(sim.state.research)+str(sim.state.research_job)
	for id in sim.tiles:
		if sim.tiles[id].building!="": next_panel_signature += id+sim.tiles[id].building
	if panel!="" and (force or panel_signature!=next_panel_signature):
		panel_signature = next_panel_signature
		rebuild_panel()
	for key in live_labels:
		var l = live_labels[key]
		if not is_instance_valid(l): continue
		if key=="migration": l.text = sim.migration_status()
		elif key=="workforce": l.text = "%d assigned · %d available" % [sim.assigned(),sim.free_workers()]
		elif key.begins_with("crew_"):
			var id = key.trim_prefix("crew_")
			if sim.tiles.has(id): l.text = "%d/%d" % [sim.tiles[id].workers,Rules.BUILDINGS[sim.tiles[id].building].crew]
		elif key.begins_with("home_"):
			var id = key.trim_prefix("home_")
			l.text = "Cottage %s   %d / 4 residents" % [id,sim.tiles[id].residents]
		elif key.begins_with("research_gate_"):
			var kind = key.trim_prefix("research_gate_")
			var error = sim.research_error(kind)
			l.text = error
			if is_instance_valid(buttons.get("research_"+kind)): buttons["research_"+kind].disabled = error!=""
		elif key=="research_time" and sim.state.research_job!="":
			l.text = "%d s remaining" % ceili(Rules.RESEARCH[sim.state.research_job].seconds-sim.state.research_progress)
	if research_bar!=null and is_instance_valid(research_bar) and sim.state.research_job!="":
		research_bar.value = 100.0*sim.state.research_progress/Rules.RESEARCH[sim.state.research_job].seconds
	if inspector.visible:
		var detail = inspector_body.get_node_or_null("WorkStatus")
		if detail: detail.text = sim.work_status(selected)
		var job = inspector_body.get_node_or_null("JobProgress")
		if job: job.value = sim.progress(selected)*100
	for kind in ["inspect","claim","road","build","town","research"]:
		var b = buttons.get("dock_"+kind)
		if is_instance_valid(b):
			b.add_theme_stylebox_override("normal",style(Color("cfdabf") if game.tool==kind or panel==kind else Color("e7e8d9"),8,LINE))

func toast(message: String) -> void:
	toast_label.text = message
	toast_time = 7.0
	toast_panel.visible = true

func _process(delta: float) -> void:
	toast_time = maxf(0,toast_time-delta)
	if toast_panel: toast_panel.visible = toast_time>0
	if root and inspector and inspector.visible:
		inspector.size.y = minf(maxf(180,inspector_body.get_combined_minimum_size().y+20),root.size.y-330)
	if root and left and left.visible:
		left.size.y = minf(maxf(180,left_body.get_combined_minimum_size().y+20),root.size.y-330)
