extends RefCounted

var cols: int
var rows: int
var cell: float
var chunk_size: int
var floor_rows: int
var chunks_x: int
var chunks_y: int
var revision := 0
var solid_count := 0
var rebuild_us := 0
var dirty_count := 0
var digest := ""
var chunk_revisions := PackedInt32Array()
var rectangles: Array[Array] = []
var _cells := PackedByteArray()

func _init(width: int, height: int, unit: float, chunk: int, base: int, fill: Callable) -> void:
	cols = width
	rows = height
	cell = unit
	chunk_size = chunk
	floor_rows = base
	chunks_x = ceili(float(cols) / chunk_size)
	chunks_y = ceili(float(rows) / chunk_size)
	_cells.resize(cols * rows)
	chunk_revisions.resize(chunks_x * chunks_y)
	for y in range(rows):
		for x in range(cols):
			if y < floor_rows or fill.call(x, y):
				_cells[x + y * cols] = 1
				solid_count += 1
	var dirty: Array[int] = []
	for i in range(chunks_x * chunks_y):
		rectangles.append([])
		dirty.append(i)
	_rebuild(dirty)

func solid(x: int, y: int) -> bool:
	return x >= 0 and x < cols and y >= 0 and y < rows and _cells[x + y * cols] == 1

func solid_at(point: Vector2) -> bool:
	return solid(floori(point.x / cell), floori(point.y / cell))

func cut_many(cuts: Array) -> int:
	var before := solid_count
	var dirty := {}
	for cut: Dictionary in cuts:
		_remove(cut["a"], cut["b"], float(cut["r"]), dirty)
	dirty_count = dirty.size()
	rebuild_us = 0
	if dirty_count > 0:
		revision += 1
		var chunks := dirty.keys()
		chunks.sort()
		_rebuild(chunks)
	return before - solid_count

func query(region: Rect2) -> Array[Rect2]:
	var found: Array[Rect2] = []
	var pitch := cell * chunk_size
	var first_x := maxi(0, floori((region.position.x - 0.00001) / pitch))
	var first_y := maxi(0, floori((region.position.y - 0.00001) / pitch))
	var last_x := mini(chunks_x - 1, floori(region.end.x / pitch))
	var last_y := mini(chunks_y - 1, floori(region.end.y / pitch))
	for cy in range(first_y, last_y + 1):
		for cx in range(first_x, last_x + 1):
			for rect: Rect2 in rectangles[cx + cy * chunks_x]:
				if rect.intersects(region, true):
					found.append(rect)
	return found

func _remove(a: Vector2, b: Vector2, radius: float, dirty: Dictionary) -> void:
	var lo := a.min(b) - Vector2.ONE * radius
	var hi := a.max(b) + Vector2.ONE * radius
	var start_x := maxi(0, floori(lo.x / cell))
	var start_y := maxi(floor_rows, floori(lo.y / cell))
	var end_x := mini(cols - 1, floori(hi.x / cell))
	var end_y := mini(rows - 1, floori(hi.y / cell))
	var axis := b - a
	var length_squared := axis.length_squared()
	for y in range(start_y, end_y + 1):
		for x in range(start_x, end_x + 1):
			if not solid(x, y):
				continue
			var center := (Vector2(x, y) + Vector2(0.5, 0.5)) * cell
			var t := 0.0
			if length_squared > 0.0:
				t = clampf((center - a).dot(axis) / length_squared, 0.0, 1.0)
			if center.distance_squared_to(a + axis * t) <= radius * radius:
				_cells[x + y * cols] = 0
				solid_count -= 1
				@warning_ignore("integer_division")
				var index := x / chunk_size + y / chunk_size * chunks_x
				dirty[index] = true

func _rebuild(dirty: Array) -> void:
	var start := Time.get_ticks_usec()
	for chunk: int in dirty:
		rectangles[chunk] = _build_rectangles(chunk)
		chunk_revisions[chunk] = revision
	rebuild_us = Time.get_ticks_usec() - start
	var hash_context := HashingContext.new()
	hash_context.start(HashingContext.HASH_SHA256)
	hash_context.update(_cells)
	digest = hash_context.finish().hex_encode()

func _build_rectangles(chunk: int) -> Array[Rect2]:
	var x0 := (chunk % chunks_x) * chunk_size
	@warning_ignore("integer_division")
	var y0 := (chunk / chunks_x) * chunk_size
	var x1 := mini(x0 + chunk_size, cols)
	var y1 := mini(y0 + chunk_size, rows)
	var used := PackedInt32Array()
	used.resize(y1 - y0)
	var result: Array[Rect2] = []
	for y in range(y0, y1):
		var x := x0
		while x < x1:
			if not solid(x, y) or used[y - y0] & (1 << (x - x0)):
				x += 1
				continue
			var right := x + 1
			while right < x1 and solid(right, y) and not (used[y - y0] & (1 << (right - x0))):
				right += 1
			var mask := ((1 << (right - x)) - 1) << (x - x0)
			var top := y + 1
			while top < y1 and not (used[top - y0] & mask) and _row_solid(x, right, top):
				top += 1
			for row in range(y, top):
				used[row - y0] |= mask
			result.append(Rect2(Vector2(x, y) * cell, Vector2(right - x, top - y) * cell))
			x = right
	return result

func _row_solid(left: int, right: int, y: int) -> bool:
	for x in range(left, right):
		if not solid(x, y):
			return false
	return true
