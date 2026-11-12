extends RefCounted

const Grid = preload("res://terrain/grid.gd")

const VERSION := "g2-02.1"
const WIDTH := 40.0
const HEIGHT := 18.0
const CELL := 0.25
const COLS := 160
const ROWS := 72
const CHUNK := 16
const FLOOR_ROWS := 2
const HZ := 60
const STEP := 1.0 / HZ
const GRAVITY := 9.81
const MAX_SHOTS := 4
const MAX_SPEED := 29.0
const RADIUS := 0.095
const BLAST := 1.65
const DRILL := 0.78
const PIVOT := 0.39
const BARREL := 0.82
const VOLLEY_TICK := 45
const VOLLEY_ANGLE := 7
const SANDBOX := 0
const VOLLEY := 1

static func make_terrain(scenario: int) -> Grid:
	if scenario == VOLLEY:
		return Grid.new(COLS, ROWS, CELL, CHUNK, FLOOR_ROWS,
			func(x: int, y: int) -> bool: return y < 16 or (x >= 68 and x < 87 and y < 47))
	var grid := Grid.new(COLS, ROWS, CELL, CHUNK, FLOOR_ROWS, _sandbox_solid)
	grid.cut_many([
		{"a": Vector2(17.3, 3.12), "b": Vector2(22.8, 3.5), "r": 0.68},
		{"a": Vector2(19.1, 3.6), "b": Vector2(20.8, 4.5), "r": 0.97},
	])
	return grid

static func spawns(scenario: int) -> Array[Vector2]:
	if scenario == VOLLEY:
		return [Vector2(12, 4.57), Vector2(10, 4.57), Vector2(8, 4.57), Vector2(6, 4.57)]
	return [Vector2(7, 5.5), Vector2(31, 6.7)]

static func direction(angle: int) -> Vector2:
	match clampi(angle, 0, 180):
		0: return Vector2.RIGHT
		90: return Vector2(0, 1)
		180: return Vector2.LEFT
		_: return Vector2.from_angle(deg_to_rad(angle))

static func _sandbox_solid(x: int, y: int) -> bool:
	var wx := (x + 0.5) * CELL
	var surface := 4.45 + 1.4 * _peak(wx, 29.0, 8.0)
	surface += 4.35 * _peak(wx, 20.5, 6.8) + 0.75 * _peak(wx, 2.5, 5.0)
	return (y + 0.5) * CELL < surface

static func _peak(x: float, center: float, width: float) -> float:
	var t := maxf(0.0, 1.0 - absf(x - center) / width)
	return t * t * (3.0 - 2.0 * t)
