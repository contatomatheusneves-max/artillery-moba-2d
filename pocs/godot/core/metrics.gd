extends RefCounted

const MAX_SAMPLES := 12000

var samples := {"step_us": [], "collider_us": [], "frame_us": []}
var counts := {"step_us": 0, "collider_us": 0, "frame_us": 0}
var peak_dirty_chunks := 0
var first_divergence := -1
var divergences := 0
var impact_log_diverged := false

func sample(series: String, duration: int) -> void:
	var values: Array = samples[series]
	var count := int(counts[series])
	if values.size() < MAX_SAMPLES:
		values.append(duration)
	else:
		values[count % MAX_SAMPLES] = duration
	counts[series] = count + 1

func mismatch(tick: int) -> void:
	if first_divergence < 0:
		first_divergence = tick
	divergences += 1

func snapshot() -> Dictionary:
	var result := {
		"engine": Engine.get_version_info()["string"],
		"platform": OS.get_name(),
		"display": DisplayServer.get_name(),
		"cpu": OS.get_processor_name(),
		"debug": OS.is_debug_build(),
		"peak_dirty_chunks": peak_dirty_chunks,
		"first_divergence_tick": first_divergence,
		"divergences": divergences,
		"impact_log_diverged": impact_log_diverged,
	}
	var memory := Performance.get_monitor(Performance.MEMORY_STATIC)
	result["engine_static_memory_bytes"] = int(memory) if memory > 0 else null
	for series: String in samples:
		var values: Array = samples[series].duplicate()
		values.sort()
		if values.is_empty():
			continue
		result[series] = {
			"samples": values.size(), "total_calls": counts[series],
			"median": values[ceili(values.size() * 0.5) - 1],
			"p95": values[ceili(values.size() * 0.95) - 1],
			"max": values.back(),
		}
	return result
