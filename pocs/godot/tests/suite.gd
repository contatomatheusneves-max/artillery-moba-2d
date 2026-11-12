extends RefCounted

const Arena = preload("res://core/arena.gd")
const Simulation = preload("res://core/simulation.gd")
const Clock = preload("res://core/fixed_clock.gd")
const Trace = preload("res://replay/trace.gd")
const Grid = preload("res://terrain/grid.gd")
const Ballistics = preload("res://projectile/ballistics.gd")
const ShotQueries = preload("res://physics/shot_queries.gd")

var failures: Array[String] = []
var sample_metrics := {}

func run() -> Dictionary:
	var cases := {
		"01 Cratera, túnel e colisão": _crater_and_tunnel,
		"02 Apoio, movimento e queda": _tank_support,
		"03 Salva, reconsulta e impactos simultâneos": _volley,
		"04 Potência zero e colisão com tanque": _zero_and_tank_hit,
		"05 Replay JSON em cadências de 30 e 144 Hz": _replay,
		"06 Base indestrutível e apoio físico": _protected_floor,
		"07 Contenção dos dois lados": _walls,
		"08 Projétil rápido e tanque móvel": _moving_target,
	}
	var lines := PackedStringArray()
	var passed := 0
	for name: String in cases:
		failures.clear()
		var test: Callable = cases[name]
		var completed: Variant = test.call()
		_check(completed == true, "execução interrompida antes do fim do teste")
		if failures.is_empty():
			lines.append("PASS " + name)
			passed += 1
		else:
			lines.append("FAIL " + name)
			for failure in failures:
				lines.append("  " + failure)
	lines.append("%d/%d testes aprovados." % [passed, cases.size()])
	return {"passed": passed, "total": cases.size(), "lines": lines, "metrics": sample_metrics}

func _crater_and_tunnel() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	var start := Vector2(18.125, 13.0)
	var end := Vector2(18.125, 9.0)
	var before := _shot(world, start, end)
	var tunnel := Vector2(19.125, 7.625)
	_check(not before.is_empty(), "superfície inicial sem colisor")
	_check(_collision_at(world.terrain, tunnel), "parede inicial não ocupava o futuro túnel")

	var top := Vector2(18.125, 11.7)
	world.step(Trace.drill(0, top, top))
	var after := _shot(world, start, end)
	_check(not after.is_empty() and not before.is_empty() and after["point"].y < before["point"].y - 0.45,
		"cratera não rebaixou a superfície de colisão")
	world.step(Trace.drill(1, Vector2(15, 7.5), Vector2(23, 7.5)))
	_check(not _collision_at(world.terrain, tunnel), "colisor ainda bloqueia a cavidade")
	_check(_collision_at(world.terrain, Vector2(19.125, 9.125)), "teto removido indevidamente")
	_check(_collision_at(world.terrain, Vector2(19.125, 6.125)), "piso do túnel removido")
	_check(_shot(world, Vector2(15, 7.5), Vector2(23, 7.5)).is_empty(), "varredura não atravessou o túnel")
	return true

func _tank_support() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	var tank := world.tanks[0]
	_advance(world, 70)
	var start: Vector2 = tank.position
	_check(tank.grounded and absf(start.y - 4.33) < 0.03, "tanque não estabilizou no chão")
	world.step(Trace.command(world.tick, 38, 74, 1))
	_advance(world, 20)
	_check(tank.position.x > start.x + 0.15 and tank.position.y > 4.20, "movimento atravessou o chão ou parou")
	var x: float = tank.position.x
	world.step(Trace.drill(world.tick, Vector2(x - 1.1, 4), Vector2(x + 1.1, 4)))
	_advance(world, 50)
	_check(tank.grounded and tank.position.y > 3.3 and tank.position.y < start.y - 0.35,
		"tanque não caiu até o apoio seguinte")
	return true

func _volley() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	_advance(world, Arena.VOLLEY_TICK)
	world.step(_volley_command(world.tick))
	_check(world.weapon.shots.size() == 4 and world.peak_shots == 4, "quatro projéteis não coexistiram")
	for shell in world.weapon.shots:
		_check(shell.age == 1, "projétil lançado em outro passo")
	_advance(world, 90)
	var hits: Array[Dictionary] = []
	for hit: Dictionary in world.trace["impacts"]:
		if int(hit["target"]) == -1 and float(hit["x"]) >= 16.9:
			hits.append(hit)
	_check(hits.size() >= 2, "parede teve menos de dois impactos")
	if hits.size() >= 2:
		var first := hits[0]
		_check(int(first["terrain_after"]) > int(first["terrain_before"]), "primeiro impacto não deformou")
		_check(hits.slice(1).any(func(hit: Dictionary) -> bool:
			return float(hit["x"]) > float(first["x"]) + 0.5 and (
				int(hit["terrain_before"]) >= int(first["terrain_after"]))),
			"projétil posterior não atingiu a parede mais fundo após o primeiro corte")
	var left := Arena.make_terrain(Arena.VOLLEY)
	var right := Arena.make_terrain(Arena.VOLLEY)
	var cuts := [
		{"a": Vector2(17.0, 5.3), "b": Vector2(17.0, 5.3), "r": Arena.BLAST},
		{"a": Vector2(18.1, 5.3), "b": Vector2(18.1, 5.3), "r": Arena.BLAST},
	]
	left.cut_many(cuts)
	cuts.reverse()
	right.cut_many(cuts)
	_check(left.digest == right.digest and left.revision == 1 and right.revision == 1,
		"união dos cortes simultâneos dependeu da ordem")
	var gun := Ballistics.new()
	var groups: Array[int] = []
	for id in range(2):
		gun.launch(id, Vector2(12, 10), 0, 100)
	gun.step(
		func(a: Vector2, b: Vector2, _cursor: float, _owner: int) -> Dictionary:
			return {"t": 0.5, "target": -1, "point": a.lerp(b, 0.5)},
		func(group: Array[Dictionary]) -> void: groups.append(group.size()))
	_check(groups.size() == 1 and groups[0] == 2 and gun.shots.is_empty(),
		"dois impactos no mesmo instante não formaram um grupo")
	return _same_tick_resweep()

func _same_tick_resweep() -> bool:
	var gun := Ballistics.new()
	gun.launch(0, Vector2(1.12 - Arena.BARREL, 5), 0, 100)
	gun.launch(1, Vector2(0.99 - Arena.BARREL, 5), 0, 100)
	var wall := [1.25]
	var impacts: Array = []
	gun.step(
		func(a: Vector2, b: Vector2, _cursor: float, _owner: int) -> Dictionary:
			var t := (float(wall[0]) - Arena.RADIUS - a.x) / (b.x - a.x)
			return {"t": t, "target": -1, "point": Vector2(wall[0], 5)} if t >= 0.0 and t <= 1.0 else {},
		func(group: Array[Dictionary]) -> void:
			impacts.append_array(group)
			wall[0] = 1.40)
	_check(impacts.size() == 2 and impacts[0]["shot"] == 0 and impacts[1]["shot"] == 1 and (
		impacts[0]["fraction"] < impacts[1]["fraction"] and impacts[1]["point"].x > 1.39),
		"segundo impacto do mesmo passo usou a parede antiga")
	return true

func _zero_and_tank_hit() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	_advance(world, 70)
	world.step(_fire(world.tick, 180, 0))
	_check(world.weapon.shots.is_empty(), "potência zero criou projétil")
	world.step(_fire(world.tick, 180, 60))
	_advance(world, 18)
	var impacts: Array = world.trace["impacts"]
	_check(impacts.size() == 1 and int(impacts[0]["target"]) == 1 and world.tanks.size() == 4,
		"tiro a 180° não registrou o tanque vizinho ou removeu um tanque")
	_check(Arena.direction(0) == Vector2.RIGHT and Arena.direction(90) == Vector2(0, 1) and (
		Arena.direction(180) == Vector2.LEFT), "direções globais incorretas")
	return true

func _replay() -> bool:
	var plan := [_volley_command(Arena.VOLLEY_TICK),
		Trace.drill(94, Vector2(17, 8), Vector2(23, 8))]
	var original := _run_at(30, plan)
	var recording := Trace.decode(JSON.stringify(original.trace, "", true, true))
	_check(not recording.is_empty(), "registro JSON não voltou a ser carregado")
	if recording.is_empty():
		return false
	var repeated := _run_at(144, recording["commands"])
	var first := -1
	var differences := 0
	var count: int = original.trace["hashes"].size()
	for i in range(mini(count, repeated.trace["hashes"].size())):
		if original.trace["hashes"][i] != repeated.trace["hashes"][i]:
			if first < 0: first = i
			differences += 1
	_check(count == 180 and repeated.tick == 180, "cadência mudou o número de passos")
	_check(differences == 0, "primeiro hash divergente no passo %d" % first)
	_check(not recording["impacts"].is_empty() and Trace.impacts_match(repeated.trace["impacts"],
		recording["impacts"]), "registro de impactos divergiu")
	sample_metrics = repeated.metrics.snapshot()
	sample_metrics["workload"] = "Replay 180 passos, cadência sintética de 144 Hz, quatro tiros e uma escavação"
	sample_metrics["terrain_revision"] = repeated.terrain.revision
	sample_metrics["first_divergence_tick"] = first
	sample_metrics["divergences"] = differences
	return true

func _protected_floor() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	var floor_height := Arena.FLOOR_ROWS * Arena.CELL
	world.step(Trace.drill(0, Vector2(12, floor_height), Vector2(12, floor_height)))
	world.step(Trace.drill(1, Vector2(12, -1), Vector2(12, 5)))
	var hit := _shot(world, Vector2(12, 1.05), Vector2(12, -1))
	_check(not hit.is_empty() and absf(hit["point"].y - floor_height) < 0.04,
		"colisor do fundo foi perdido ou ficou mais alto após cortes profundos")
	_advance(world, 100)
	_check(world.tanks[0].grounded and absf(world.tanks[0].position.y - 0.83) < 0.05,
		"tanque não foi sustentado pela camada protegida")
	var base := Arena.make_terrain(Arena.SANDBOX)
	var removed := base.cut_many([{"a": Vector2(-1, 0.12), "b": Vector2(41, 0.6), "r": 0.6}])
	_check(removed > 0, "túnel ao longo da base não removeu solo acima dela")
	for x in range(Arena.COLS):
		_check(base.solid(x, 0) and base.solid(x, 1) and not base.solid(x, 2),
			"corte violou a proteção ou bloqueou a faixa escavável na coluna %d" % x)
	var revision := base.revision
	var point := Vector2(0.125, 0.125)
	_check(base.cut_many([{"a": point, "b": point, "r": 0.2}]) == 0 and base.revision == revision,
		"um corte inteiramente protegido gerou revisão")
	return true

func _walls() -> bool:
	for direction in [-1, 1]:
		var world := Simulation.new(Arena.VOLLEY)
		var tank := world.tanks[0]
		var edge := 0.0 if direction < 0 else Arena.WIDTH
		tank.position = Vector2(edge - direction * 1.5, 4.57)
		_advance(world, 45)
		world.step(Trace.command(world.tick, 38, 74, direction))
		for i in range(90):
			world.step()
			var hull := tank.hull_at(tank.position)
			_check(hull.position.x >= -0.01 and hull.end.x <= Arena.WIDTH + 0.01 and tank.position.y > 4.2,
				"casco saiu da arena na direção %d" % direction)
		var hull := tank.hull_at(tank.position)
		var outer := hull.position.x if direction < 0 else hull.end.x
		_check(absf(outer - edge) <= 0.02 and absf(tank.velocity.x) < 0.03, "parede não bloqueou o avanço")
	return true

func _moving_target() -> bool:
	var world := Simulation.new(Arena.VOLLEY)
	_advance(world, 45)
	var shooter := world.tanks[0]
	var target := world.tanks[1]
	shooter.position = Vector2(27, 4.34)
	var initial := Vector2(31, 5.45)
	target.position = initial
	target.velocity = Vector2(-2, -6)
	_check(target.hull_at(initial).position.y > shooter.pivot().y + Arena.RADIUS,
		"tanque já estava na altura do tiro antes de se mover")
	world.step(_fire(world.tick, 0, 100))
	_check(world.weapon.shots.size() == 1 and absf(world.weapon.shots[0].velocity.x - Arena.MAX_SPEED) < 0.001,
		"projétil não partiu à velocidade máxima")
	for i in range(15):
		if not world.trace["impacts"].is_empty(): break
		world.step()
	var hits: Array = world.trace["impacts"]
	_check(hits.size() == 1 and int(hits[0]["target"]) == target.id and target.velocity.y < -3.0 and (
		target.position.x < initial.x - 0.08 and target.position.y < initial.y - 0.4),
		"tiro não atingiu o tanque ainda em movimento")

	target.previous = Vector2(30.1, 4.0)
	target.position = Vector2(30.1, 7.0)
	var crossing := ShotQueries.first_hit(world.terrain, [target], Vector2(29.85, 5.5), Vector2(30.35, 5.5), 0.0, 0)
	_check(not crossing.is_empty() and int(crossing["target"]) == target.id and (
		float(crossing["t"]) > 0.0 and float(crossing["t"]) < 1.0),
		"varredura relativa perdeu um cruzamento inteiramente entre poses")
	return true

func _shot(world: Simulation, a: Vector2, b: Vector2) -> Dictionary:
	return ShotQueries.first_hit(world.terrain, [], a, b, 0.0, -1)

func _collision_at(grid: Grid, point: Vector2) -> bool:
	for rect in grid.query(Rect2(point - Vector2.ONE * 0.001, Vector2.ONE * 0.002)):
		if rect.has_point(point):
			return true
	return false

func _run_at(rate: int, plan: Array) -> Simulation:
	var world := Simulation.new(Arena.VOLLEY)
	var clock := Clock.new()
	var next := [0]
	var tick := func() -> void:
		var index: int = next[0]
		if index < plan.size() and int(plan[index]["tick"]) == world.tick:
			world.step(plan[index])
			next[0] = index + 1
		else:
			world.step()
	for frame in range(rate * 3):
		clock.advance(1.0 / rate, tick)
	return world

func _advance(world: Simulation, count: int) -> void:
	for i in range(count):
		world.step()

func _volley_command(tick: int) -> Dictionary:
	var command := Trace.command(tick, Arena.VOLLEY_ANGLE, 100)
	command["volley"] = true
	return command

func _fire(tick: int, angle: int, power: int) -> Dictionary:
	var command := Trace.command(tick, angle, power)
	command["fire"] = true
	return command

func _check(condition: bool, message: String) -> void:
	if not condition and not failures.has(message):
		failures.append(message)
