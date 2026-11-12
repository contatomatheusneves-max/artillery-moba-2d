extends RefCounted

const Arena = preload("res://core/arena.gd")
const FILE := "user://g2-02-replay.json"

static func create(scenario: int) -> Dictionary:
	return {
		"experiment": Arena.VERSION,
		"engine": Engine.get_version_info()["string"],
		"platform": OS.get_name(),
		"scenario": scenario,
		"commands": [],
		"impacts": [],
		"hashes": [],
	}

static func command(tick: int, angle: int, power: int, drive: int = 0) -> Dictionary:
	return {"tick": tick, "angle": angle, "power": power, "drive": drive}

static func drill(tick: int, from: Vector2, to: Vector2, angle: int = 38, power: int = 74) -> Dictionary:
	var entry := command(tick, angle, power)
	entry["cut"] = [roundi(from.x * 100), roundi(from.y * 100),
		roundi(to.x * 100), roundi(to.y * 100)]
	return entry

static func write_json(path: String, payload: Dictionary) -> bool:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(payload, "\t", true, true))
	return file.get_error() == OK

static func read_replay(path: String = FILE) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	return decode(FileAccess.get_file_as_string(path))

static func decode(text: String) -> Dictionary:
	var raw: Variant = JSON.parse_string(text)
	if not raw is Dictionary or not valid(raw):
		return {}
	return raw

static func valid(raw: Dictionary) -> bool:
	if raw.get("experiment") != Arena.VERSION:
		return false
	if not _whole(raw.get("scenario"), Arena.SANDBOX, Arena.VOLLEY):
		return false
	if not raw.get("hashes") is Array or not raw.get("commands") is Array or not raw.get("impacts") is Array:
		return false
	var hashes: Array = raw["hashes"]
	if hashes.is_empty():
		return false
	for digest in hashes:
		if not digest is String or digest.length() != 64:
			return false
	var last := -1
	for entry in raw["commands"]:
		if not entry is Dictionary:
			return false
		if not _whole(entry.get("tick"), 0, hashes.size() - 1) or int(entry["tick"]) <= last:
			return false
		last = int(entry["tick"])
		if not _whole(entry.get("angle"), 0, 180) or not _whole(entry.get("power"), 0, 100):
			return false
		if not _whole(entry.get("drive"), -1, 1):
			return false
		if entry.has("fire") and not entry["fire"] is bool:
			return false
		if entry.has("volley") and not entry["volley"] is bool:
			return false
		if entry.has("cut"):
			var coords: Variant = entry["cut"]
			if not coords is Array or coords.size() != 4:
				return false
			for coordinate in coords:
				if not _whole(coordinate, -100000, 100000):
					return false
	return true

static func _whole(value: Variant, minimum: int, maximum: int) -> bool:
	return (value is int or value is float) and is_finite(float(value)) and (
		float(value) >= minimum and float(value) <= maximum and float(value) == int(value))

static func impacts_match(actual: Array, recorded: Array) -> bool:
	# JSON numbers come back as floats; reparse actual values before comparison.
	return JSON.parse_string(JSON.stringify(actual, "", true, true)) == recorded
