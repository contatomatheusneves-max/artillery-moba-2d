extends RefCounted

const Arena = preload("res://core/arena.gd")
const Tank = preload("res://physics/tank.gd")
const Grid = preload("res://terrain/grid.gd")
const ShotQueries = preload("res://physics/shot_queries.gd")
const Ballistics = preload("res://projectile/ballistics.gd")
const Trace = preload("res://replay/trace.gd")
const Metrics = preload("res://core/metrics.gd")
const WALLS := [Rect2(-1, -1, 1, Arena.HEIGHT + 2), Rect2(Arena.WIDTH, -1, 1, Arena.HEIGHT + 2)]

var scenario: int
var terrain: Grid
var tanks: Array[Tank] = []
var weapon := Ballistics.new()
var metrics := Metrics.new()
var trace: Dictionary
var tick := 0
var power := 74
var drive := 0
var peak_shots := 0

func _init(selected: int) -> void:
	scenario = selected
	terrain = Arena.make_terrain(scenario)
	for spawn in Arena.spawns(scenario):
		tanks.append(Tank.new(tanks.size(), spawn))
	trace = Trace.create(scenario)

func step(command: Dictionary = {}) -> void:
	var start := Time.get_ticks_usec()
	if not command.is_empty():
		assert(int(command["tick"]) == tick)
		trace["commands"].append(command.duplicate(true))
		_apply(command)

	for tank in tanks:
		tank.previous = tank.position
	for tank in tanks:
		tank.step(drive if tank.id == 0 else 0, _obstacles)
	weapon.step(_shot_hit, _resolve)
	trace["hashes"].append(_state_hash())
	tick += 1
	metrics.sample("step_us", Time.get_ticks_usec() - start)

func _apply(command: Dictionary) -> void:
	tanks[0].angle = clampi(int(command["angle"]), 0, 180)
	power = clampi(int(command["power"]), 0, 100)
	drive = clampi(int(command["drive"]), -1, 1)
	if command.has("cut"):
		var points: Array = command["cut"]
		_cut([{"a": Vector2(points[0], points[1]) / 100.0,
			"b": Vector2(points[2], points[3]) / 100.0, "r": Arena.DRILL}])

	if command.get("volley", false) and scenario == Arena.VOLLEY:
		for tank in tanks:
			tank.angle = Arena.VOLLEY_ANGLE
			weapon.launch(tank.id, tank.pivot(), tank.angle, 100)
		power = 100
	elif command.get("fire", false):
		weapon.launch(0, tanks[0].pivot(), tanks[0].angle, power)
	peak_shots = maxi(peak_shots, weapon.shots.size())

func _obstacles(region: Rect2, owner: int) -> Array[Rect2]:
	var candidates := terrain.query(region)
	for wall: Rect2 in WALLS:
		if wall.intersects(region, true):
			candidates.append(wall)
	for tank in tanks:
		if tank.id != owner:
			var hull := tank.hull_at(tank.position)
			if hull.intersects(region, true):
				candidates.append(hull)
	return candidates

func _shot_hit(origin: Vector2, end: Vector2, cursor: float, owner: int) -> Dictionary:
	return ShotQueries.first_hit(terrain, tanks, origin, end, cursor, owner)

func _resolve(group: Array[Dictionary]) -> void:
	var before := terrain.revision
	var cuts: Array[Dictionary] = []
	for hit in group:
		cuts.append({"a": hit["point"], "b": hit["point"], "r": Arena.BLAST})
	_cut(cuts)
	for hit in group:
		var point: Vector2 = hit["point"]
		trace["impacts"].append({
			"tick": tick, "shot": hit["shot"], "target": hit["target"],
			"fraction": hit["fraction"], "x": point.x, "y": point.y,
			"terrain_before": before, "terrain_after": terrain.revision,
		})

func _cut(cuts: Array) -> void:
	if terrain.cut_many(cuts) > 0:
		metrics.sample("collider_us", terrain.rebuild_us)
		metrics.peak_dirty_chunks = maxi(metrics.peak_dirty_chunks, terrain.dirty_count)

func _state_hash() -> String:
	var state := [scenario, tick, power, drive, peak_shots, terrain.revision, terrain.digest, weapon.hash_state()]
	for tank in tanks:
		state.append([tank.id, tank.angle, tank.position.x, tank.position.y,
			tank.velocity.x, tank.velocity.y, tank.grounded])
	return JSON.stringify(state, "", true, true).sha256_text()
