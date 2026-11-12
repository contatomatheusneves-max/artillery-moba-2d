extends RefCounted

const Arena = preload("res://core/arena.gd")
const TIE := 0.00001

class Shell:
	var id: int
	var owner: int
	var position: Vector2
	var velocity: Vector2
	var endpoint: Vector2
	var age := 0

var shots: Array[Shell] = []
var next_id := 0

func launch(owner: int, pivot: Vector2, angle: int, power: int) -> bool:
	if power <= 0 or shots.size() >= Arena.MAX_SHOTS:
		return false
	var direction := Arena.direction(angle)
	var shell := Shell.new()
	shell.id = next_id
	next_id += 1
	shell.owner = owner
	shell.position = pivot + direction * Arena.BARREL
	shell.velocity = direction * (Arena.MAX_SPEED * clampi(power, 0, 100) / 100.0)
	shots.append(shell)
	return true

func step(query: Callable, resolve: Callable) -> void:
	for shell in shots:
		shell.velocity.y -= Arena.GRAVITY * Arena.STEP
		shell.endpoint = shell.position + shell.velocity * Arena.STEP

	var cursor := 0.0
	while not shots.is_empty():
		var candidates: Array[Dictionary] = []
		var first := INF
		for shell in shots:
			var origin := shell.position.lerp(shell.endpoint, cursor)
			var hit: Dictionary = query.call(origin, shell.endpoint, cursor, shell.owner)
			if hit.is_empty():
				continue
			var time := lerpf(cursor, 1.0, float(hit["t"]))
			hit["fraction"] = time
			hit["shot"] = shell.id
			first = minf(first, time)
			candidates.append(hit)
		if candidates.is_empty():
			break

		var group: Array[Dictionary] = []
		var impacted := {}
		for hit in candidates:
			if float(hit["fraction"]) <= first + TIE:
				group.append(hit)
				impacted[hit["shot"]] = true
		for i in range(shots.size() - 1, -1, -1):
			if impacted.has(shots[i].id):
				shots.remove_at(i)
		# The callback rebuilds collision geometry before the next sweep.
		resolve.call(group)
		cursor = first

	for i in range(shots.size() - 1, -1, -1):
		var shell := shots[i]
		shell.position = shell.endpoint
		shell.age += 1
		if shell.age >= Arena.HZ * 12 or shell.position.x < -4.0 or (
				shell.position.x > Arena.WIDTH + 4.0 or shell.position.y < -4.0):
			shots.remove_at(i)

func hash_state() -> Array:
	var state := [next_id]
	for shell in shots:
		state.append([shell.id, shell.owner, shell.position.x, shell.position.y,
			shell.velocity.x, shell.velocity.y, shell.age])
	return state
