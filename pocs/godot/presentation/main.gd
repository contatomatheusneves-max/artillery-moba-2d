extends Node2D

const Arena = preload("res://core/arena.gd")
const Simulation = preload("res://core/simulation.gd")
const Clock = preload("res://core/fixed_clock.gd")
const Trace = preload("res://replay/trace.gd")
const ArenaView = preload("res://presentation/arena_view.gd")
const Hud = preload("res://presentation/hud.gd")

var world: Simulation
var view := ArenaView.new()
var hud := Hud.new()
var clock := Clock.new()
var replay := {}
var replay_index := 0
var pending_cut := []
var anchor: Variant
var fire := false
var paused := false
var slow := false
var notice := ""
var last_frame := 0

func _ready() -> void:
	add_child(view)
	var overlay := CanvasLayer.new()
	add_child(overlay)
	overlay.add_child(hud)
	_start(Arena.SANDBOX)

func _start(scenario: int) -> void:
	world = Simulation.new(scenario)
	view.set_world(world)
	hud.world = world
	clock.reset()
	anchor = null
	pending_cut.clear()
	replay.clear()
	replay_index = 0
	fire = false
	paused = false
	last_frame = 0
	notice = "Salva programada para o passo 45." if scenario == Arena.VOLLEY else ""

func _process(delta: float) -> void:
	var now := Time.get_ticks_usec()
	if last_frame > 0:
		world.metrics.sample("frame_us", now - last_frame)
	last_frame = now
	if not paused:
		clock.advance(delta * (0.25 if slow else 1.0), _step_if_running, 8)
	var pointer: Variant = view.pointer_world() if replay.is_empty() else null
	view.refresh(pointer, anchor)
	hud.paused = paused
	hud.slow = slow
	hud.notice = "Marque o outro extremo do túnel. Esc cancela." if anchor is Vector2 else notice
	hud.queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		match event.physical_keycode:
			KEY_R: _start(Arena.SANDBOX)
			KEY_B: _start(Arena.VOLLEY)
			KEY_V: slow = not slow
			KEY_TAB:
				paused = not paused
				clock.reset()
			KEY_N:
				if paused: _tick()
			KEY_L: _save()
			KEY_P: _load()
			KEY_M: _save_metrics()
			KEY_SPACE:
				if replay.is_empty(): fire = true
			KEY_ESCAPE: anchor = null
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		if replay.is_empty():
			_mark_cut(view.pointer_world())

func _mark_cut(point: Variant) -> void:
	if not point is Vector2:
		return
	if anchor is Vector2:
		pending_cut = Trace.drill(world.tick, anchor, point)["cut"]
		anchor = null
	else:
		anchor = point

func _step_if_running() -> void:
	if not paused:
		_tick()

func _tick() -> void:
	if not replay.is_empty():
		_replay_tick()
		return
	var angle := clampi(world.tanks[0].angle + _axis(KEY_UP, KEY_DOWN), 0, 180)
	var power := clampi(world.power + _axis(KEY_E, KEY_Q), 0, 100)
	var drive := _axis(KEY_D, KEY_A)
	var volley := world.scenario == Arena.VOLLEY and world.tick == Arena.VOLLEY_TICK
	var shoot := fire and (world.scenario != Arena.VOLLEY or world.tick > Arena.VOLLEY_TICK)
	var command := {}
	if volley or shoot or not pending_cut.is_empty() or (
			angle != world.tanks[0].angle or power != world.power or drive != world.drive):
		command = Trace.command(world.tick, angle, power, drive)
		if volley:
			command["volley"] = true
			notice = "Quatro projéteis lançados no mesmo passo."
		elif shoot:
			command["fire"] = true
			if power == 0: notice = "Aumente a potência para disparar."
		if not pending_cut.is_empty():
			command["cut"] = pending_cut
	world.step(command)
	pending_cut = []
	fire = false

func _axis(positive: Key, negative: Key) -> int:
	return int(Input.is_physical_key_pressed(positive)) - int(Input.is_physical_key_pressed(negative))

func _save() -> void:
	if world.tick == 0:
		notice = "Avance pelo menos um passo para salvar."
	elif Trace.write_json(Trace.FILE, world.trace):
		notice = "Replay salvo. Use P para reproduzir."
		print(ProjectSettings.globalize_path(Trace.FILE))
	else:
		notice = "Falha ao salvar o replay."

func _load() -> void:
	var source := Trace.read_replay()
	if source.is_empty():
		notice = "Replay ausente ou incompatível."
		return
	_start(int(source["scenario"]))
	replay = source
	notice = "Reproduzindo comandos gravados."

func _replay_tick() -> void:
	var expected: Array = replay["hashes"]
	if world.tick >= expected.size():
		paused = true
		return
	var commands: Array = replay["commands"]
	var command := {}
	var tick := world.tick
	if replay_index < commands.size() and int(commands[replay_index]["tick"]) == tick:
		command = commands[replay_index]
		replay_index += 1
	world.step(command)
	if world.trace["hashes"][tick] != expected[tick]:
		world.metrics.mismatch(tick)
	if world.tick < expected.size():
		return
	paused = true
	world.metrics.impact_log_diverged = not Trace.impacts_match(world.trace["impacts"], replay["impacts"])
	if world.metrics.divergences > 0:
		notice = "Primeira divergência no passo %d." % world.metrics.first_divergence
	elif world.metrics.impact_log_diverged:
		notice = "Hashes coincidiram. Registro de impactos divergiu."
	else:
		notice = "Replay concluído. Hashes e impactos coincidiram."

func _save_metrics() -> void:
	var report := world.metrics.snapshot()
	report["scenario"] = world.scenario
	report["ticks"] = world.tick
	report["terrain_revision"] = world.terrain.revision
	report["viewport_px"] = [get_viewport_rect().size.x, get_viewport_rect().size.y]
	report["window_px"] = [get_window().size.x, get_window().size.y]
	report["renderer"] = RenderingServer.get_current_rendering_method()
	var path := "user://g2-02-metrics.json"
	if Trace.write_json(path, report):
		notice = "Métricas salvas. Caminho no console."
		print(ProjectSettings.globalize_path(path))
	else:
		notice = "Falha ao salvar as métricas."
