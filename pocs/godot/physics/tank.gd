extends RefCounted

const Arena = preload("res://core/arena.gd")
const Sweep = preload("res://physics/sweep.gd")
const SIZE := Vector2(0.94, 0.66)
const SPEED := 3.5
const ACCELERATION := 26.0
const STEP_UP := 0.26
const PROBE := 0.015

var id: int
var position: Vector2
var previous: Vector2
var velocity := Vector2.ZERO
var angle := 38
var grounded := false

func _init(tank_id: int, spawn: Vector2) -> void:
	id = tank_id
	position = spawn
	previous = spawn

func pivot() -> Vector2:
	return position + Vector2(0, Arena.PIVOT)

func hull_at(center: Vector2) -> Rect2:
	return Rect2(center - SIZE * 0.5, SIZE)

func step(drive: int, obstacles_in: Callable) -> void:
	grounded = _travel(position, 1, -PROBE, obstacles_in) > -PROBE + Sweep.EPS
	if grounded:
		velocity.x = move_toward(velocity.x, drive * SPEED, ACCELERATION * Arena.STEP)
	velocity.y -= Arena.GRAVITY * Arena.STEP
	var wanted := velocity.x * Arena.STEP
	var moved := _travel(position, 0, wanted, obstacles_in)
	if grounded and absf(moved - wanted) > Sweep.EPS:
		var lift := _travel(position, 1, STEP_UP, obstacles_in)
		var lifted := position + Vector2(0, lift)
		if lift >= Arena.CELL and absf(_travel(lifted, 0, wanted, obstacles_in) - wanted) <= Sweep.EPS:
			position = lifted
			moved = wanted
	position.x += moved
	if absf(moved - wanted) > Sweep.EPS:
		velocity.x = 0.0
	wanted = velocity.y * Arena.STEP
	moved = _travel(position, 1, wanted, obstacles_in)
	position.y += moved
	if absf(moved - wanted) > Sweep.EPS:
		velocity.y = 0.0
	grounded = _travel(position, 1, -PROBE, obstacles_in) > -PROBE + Sweep.EPS

func _travel(center: Vector2, axis: int, distance: float, obstacles_in: Callable) -> float:
	if is_zero_approx(distance):
		return 0.0
	var offset := Vector2.ZERO
	offset[axis] = distance
	var start := hull_at(center)
	var region := start.merge(hull_at(center + offset)).grow(Sweep.EPS)
	var obstacles: Array[Rect2] = obstacles_in.call(region, id)
	return Sweep.limit_axis(start, obstacles, axis, distance)
