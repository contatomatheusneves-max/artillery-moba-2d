extends RefCounted

const EPS := 0.000001

static func bounds(a: Vector2, b: Vector2, radius: float) -> Rect2:
	var pad := Vector2.ONE * radius
	var lo := a.min(b) - pad
	return Rect2(lo, a.max(b) + pad - lo)

static func circle_rect(a: Vector2, b: Vector2, radius: float, rect: Rect2) -> float:
	# The rounded Minkowski expansion is the union of two strips and four corner disks.
	var closest := point_on_rect(a, rect)
	var radius_sq := radius * radius
	if a.distance_squared_to(closest) <= radius_sq:
		return 0.0
	var delta := b - a
	var speed_sq := delta.length_squared()
	if speed_sq <= EPS * EPS:
		return INF
	var best := INF
	if absf(delta.x) > EPS:
		var face := rect.position.x - radius if delta.x > 0.0 else rect.end.x + radius
		var t := (face - a.x) / delta.x
		var y := a.y + t * delta.y
		if t >= 0.0 and t <= 1.0 and y >= rect.position.y and y <= rect.end.y:
			best = t
	if absf(delta.y) > EPS:
		var face := rect.position.y - radius if delta.y > 0.0 else rect.end.y + radius
		var t := (face - a.y) / delta.y
		var x := a.x + t * delta.x
		if t >= 0.0 and t <= 1.0 and x >= rect.position.x and x <= rect.end.x:
			best = minf(best, t)
	for corner: Vector2 in [rect.position, Vector2(rect.end.x, rect.position.y),
			Vector2(rect.position.x, rect.end.y), rect.end]:
		var offset := a - corner
		var projected := offset.dot(delta)
		var discriminant := projected * projected - speed_sq * (offset.length_squared() - radius_sq)
		if discriminant >= 0.0:
			var t := (-projected - sqrt(discriminant)) / speed_sq
			if t >= 0.0 and t <= 1.0:
				best = minf(best, t)
	return best

static func point_on_rect(p: Vector2, rect: Rect2) -> Vector2:
	return p.clamp(rect.position, rect.end)

static func limit_axis(body: Rect2, obstacles: Array[Rect2], axis: int, wanted: float) -> float:
	if is_zero_approx(wanted):
		return 0.0
	var cross_axis := 1 - axis
	var allowed := wanted
	for obstacle in obstacles:
		if body.end[cross_axis] <= obstacle.position[cross_axis] + EPS:
			continue
		if body.position[cross_axis] >= obstacle.end[cross_axis] - EPS:
			continue
		if wanted > 0.0 and body.end[axis] <= obstacle.position[axis] + EPS:
			allowed = minf(allowed, maxf(0.0, obstacle.position[axis] - body.end[axis]))
		elif wanted < 0.0 and body.position[axis] >= obstacle.end[axis] - EPS:
			allowed = maxf(allowed, minf(0.0, obstacle.end[axis] - body.position[axis]))
	return allowed
