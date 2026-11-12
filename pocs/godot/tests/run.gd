extends SceneTree

const Suite = preload("res://tests/suite.gd")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	print("G2-02 | Godot ", Engine.get_version_info()["string"], " | ", DisplayServer.get_name())
	var result := Suite.new().run()
	for line: String in result["lines"]:
		print(line)
	print("METRICS ", JSON.stringify(result["metrics"], "", true, true))
	quit(0 if result["passed"] == result["total"] else 1)
