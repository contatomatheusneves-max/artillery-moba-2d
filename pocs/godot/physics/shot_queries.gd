extends RefCounted

const Arena = preload("res://core/arena.gd")
const Grid = preload("res://terrain/grid.gd")
const Sweep = preload("res://physics/sweep.gd")

static func first_hit(terrain: Grid, tanks: Array, origin: Vector2, end: Vector2,
		cursor: float, owner: int) -> Dictionary:
	var best := INF
	var target := -1
	var point := Vector2.ZERO
	for rect in terrain.query(Sweep.bounds(origin, end, Arena.RADIUS)):
		var t := Sweep.circle_rect(origin, end, Arena.RADIUS, rect)
		if t < best:
			best = t
			point = Sweep.point_on_rect(origin.lerp(end, t), rect)

	for tank in tanks:
		if tank.id == owner:
			continue
		var start: Vector2 = tank.previous.lerp(tank.position, cursor)
		var movement: Vector2 = tank.position - start
		var rect: Rect2 = tank.hull_at(start)
		var relative_end := end - movement
		if not rect.intersects(Sweep.bounds(origin, relative_end, Arena.RADIUS), true):
			continue
		var t := Sweep.circle_rect(origin, relative_end, Arena.RADIUS, rect)
		if t < best:
			best = t
			target = tank.id
			point = Sweep.point_on_rect(origin.lerp(relative_end, t), rect) + movement * t

	if is_inf(best):
		return {}
	return {"t": best, "target": target, "point": point}
