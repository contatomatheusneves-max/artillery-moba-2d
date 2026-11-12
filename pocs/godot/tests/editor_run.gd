extends Control

const Suite = preload("res://tests/suite.gd")

func _ready() -> void:
	var result := Suite.new().run()
	var output: Label = $Results
	output.text = "Godot " + str(Engine.get_version_info()["string"]) + "\n\n" + "\n".join(result["lines"])
	print(output.text)
	print("METRICS ", JSON.stringify(result["metrics"], "", true, true))
