extends Node2D

const Arena = preload("res://core/arena.gd")
const Simulation = preload("res://core/simulation.gd")
const Tank = preload("res://physics/tank.gd")
const ORIGIN := Vector2(40, 606)
const SCALE := 30.0
const TEAL := Color("#7ce1ca")
const AMBER := Color("#ffc173")
const PALE := Color("#dcece8")

class Backdrop extends Node2D:
	func _draw() -> void:
		draw_rect(Rect2(0, 0, 1280, 720), Color("#162a39"))
		for band in range(24):
			var shade := Color("#203b4d").lerp(Color("#172b39"), band / 23.0)
			draw_rect(Rect2(0, band * 26, 1280, 27), shade)
		draw_circle(Vector2(1038, 206), 78, Color("#527284"))
		draw_circle(Vector2(1038, 206), 61, Color("#d6b78c"))
		draw_colored_polygon(PackedVector2Array([
			Vector2(0, 380), Vector2(123, 354), Vector2(234, 310), Vector2(374, 397),
			Vector2(536, 290), Vector2(687, 368), Vector2(835, 310), Vector2(1010, 395),
			Vector2(1151, 293), Vector2(1280, 369), Vector2(1280, 641), Vector2(0, 641),
		]), Color("#304b59"))
		draw_colored_polygon(PackedVector2Array([
			Vector2(0, 420), Vector2(205, 364), Vector2(369, 445), Vector2(566, 386),
			Vector2(726, 437), Vector2(925, 367), Vector2(1121, 422), Vector2(1280, 383),
			Vector2(1280, 641), Vector2(0, 641),
		]), Color("#213c49"))
		for i in range(6):
			var x := 75.0 + i * 193.0
			var y := 203.0 + ((i * 7) % 5) * 25.0
			draw_line(Vector2(x, y), Vector2(x + 44.0 + i * 5.0, y), Color("#537382"), 2.0)

class TerrainChunk extends Node2D:
	var boxes: Array = []
	var revision := -1

	func _draw() -> void:
		for box: Rect2 in boxes:
			var at := Vector2(40 + box.position.x * 30.0, 606 - box.end.y * 30.0)
			var tint := Color("#354451").lerp(Color("#8ba39a"), clampf(box.end.y / 12.0, 0.0, 1.0))
			draw_rect(Rect2(at, box.size * 30.0), tint)

var world: Simulation
var chunks: Array[TerrainChunk] = []
var pointer: Variant
var anchor: Variant

func _ready() -> void:
	var backdrop := Backdrop.new()
	backdrop.z_index = -2
	add_child(backdrop)

func set_world(next_world: Simulation) -> void:
	world = next_world
	for chunk in chunks:
		chunk.hide()
		chunk.queue_free()
	chunks.clear()
	for i in range(world.terrain.rectangles.size()):
		var chunk := TerrainChunk.new()
		chunk.z_index = -1
		add_child(chunk)
		chunks.append(chunk)
	refresh(null, null)

func refresh(cursor: Variant, start: Variant) -> void:
	pointer = cursor
	anchor = start
	for i in range(chunks.size()):
		var chunk := chunks[i]
		var revision := world.terrain.chunk_revisions[i]
		if chunk.revision != revision:
			chunk.revision = revision
			chunk.boxes = world.terrain.rectangles[i]
			chunk.queue_redraw()
	queue_redraw()

func pointer_world() -> Variant:
	var local := get_local_mouse_position() - ORIGIN
	var at := Vector2(local.x, -local.y) / SCALE
	if at.x >= 0.0 and at.x < Arena.WIDTH and at.y >= 0.0 and at.y < Arena.HEIGHT:
		return at
	return null

func _draw() -> void:
	if world == null:
		return
	draw_rect(Rect2(ORIGIN - Vector2(0, 15), Vector2(Arena.WIDTH * SCALE, 15)), Color("#455b65"))
	draw_line(ORIGIN - Vector2(0, 15), ORIGIN + Vector2(Arena.WIDTH * SCALE, -15), Color("#75918f"))
	for x in [ORIGIN.x, ORIGIN.x + Arena.WIDTH * SCALE]:
		draw_line(Vector2(x, 126), Vector2(x, ORIGIN.y), Color("#77949c55"), 2.0)

	for tank in world.tanks:
		_draw_tank(tank)
	for shell in world.weapon.shots:
		var at := _pixel(shell.position)
		draw_circle(at, 5.1, AMBER)
		draw_circle(at, 2.3, Color("#fff0d1"))

	var impacts: Array = world.trace["impacts"]
	for i in range(maxi(0, impacts.size() - 4), impacts.size()):
		var impact: Dictionary = impacts[i]
		var age := world.tick - int(impact["tick"])
		if age <= 22:
			var at := _pixel(Vector2(impact["x"], impact["y"]))
			var radius := lerpf(5.0, Arena.BLAST * SCALE, age / 22.0)
			var tint := Color(AMBER, 1.0 - age / 23.0)
			draw_arc(at, radius, 0.0, TAU, 32, tint, 2.0, true)

	if pointer is Vector2:
		draw_arc(_pixel(pointer), Arena.DRILL * SCALE, 0.0, TAU, 36, TEAL, 1.5, true)
	if anchor is Vector2:
		draw_arc(_pixel(anchor), Arena.DRILL * SCALE, 0.0, TAU, 36, AMBER, 2.0, true)

func _draw_tank(tank: Tank) -> void:
	var at := _pixel(tank.position)
	var accent := TEAL if tank.id == 0 else Color("#a7b6d4")
	draw_rect(Rect2(at + Vector2(-14, -1), Vector2(28, 11)), Color("#1c2731"))
	for offset in [-8.0, 0.0, 8.0]:
		draw_circle(at + Vector2(offset, 4), 2.9, Color("#71808a"))
	draw_colored_polygon(PackedVector2Array([
		at + Vector2(-13, 1), at + Vector2(13, 1), at + Vector2(8, -8), at + Vector2(-8, -8),
	]), accent)
	var pivot := _pixel(tank.pivot())
	var muzzle := _pixel(tank.pivot() + Arena.direction(tank.angle) * Arena.BARREL)
	draw_line(pivot, muzzle, accent, 4.0, true)
	draw_circle(pivot, 4.6, Color("#304653"))
	draw_circle(muzzle, 2.4, PALE)

func _pixel(point: Vector2) -> Vector2:
	return ORIGIN + Vector2(point.x, -point.y) * SCALE
