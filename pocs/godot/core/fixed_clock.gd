extends RefCounted

const Arena = preload("res://core/arena.gd")
var pending := 0.0

func advance(seconds: float, tick: Callable, budget: int = 1000000) -> void:
	pending += maxf(0.0, seconds)
	var steps := 0
	while pending + 0.0000000001 >= Arena.STEP and steps < budget:
		pending -= Arena.STEP
		tick.call()
		steps += 1

func reset() -> void:
	pending = 0.0
