extends Node2D

const Arena = preload("res://core/arena.gd")
const Simulation = preload("res://core/simulation.gd")
const BG := Color("#0e1d28ef")
const BORDER := Color("#45616b")
const PALE := Color("#e8f3ef")
const MUTED := Color("#a9bec6")
const ACCENT := Color("#7ce1ca")

var world: Simulation
var paused := false
var slow := false
var notice := ""

func _draw() -> void:
	if world == null:
		return
	_panel(Rect2(24, 23, 338, 92))
	_text(Vector2(40, 55), "Artilharia 2D", 26, PALE)
	_text(Vector2(41, 78), "Arena de salva" if world.scenario == Arena.VOLLEY else "Campo de testes", 14, MUTED)
	_text(Vector2(41, 102), "Mira %03d°    Potência %03d/100" % [world.tanks[0].angle, world.power], 17, ACCENT)

	_panel(Rect2(894, 23, 362, 92))
	var pace := "Pausado" if paused else ("0,25×" if slow else "1×")
	_text(Vector2(911, 53), "Passo %04d   ·   %s" % [world.tick, pace], 17, ACCENT)
	_text(Vector2(911, 77), "Projéteis %d/4   ·   Pico %d   ·   Terreno v%d" % [
		world.weapon.shots.size(), world.peak_shots, world.terrain.revision], 15, PALE)
	var contact := "em contato" if world.tanks[0].grounded else "sem contato"
	_text(Vector2(911, 100), "Tanque %s   ·   Células %d" % [contact, world.terrain.solid_count], 14, MUTED)

	_panel(Rect2(24, 625, 1232, 73))
	_text(Vector2(41, 646), "A/D  Mover     ↑/↓  Mirar     Q/E  Potência     Espaço  Disparar     Botão direito ×2  Escavar", 15, PALE)
	_text(Vector2(41, 668), "B  Salva     R  Reiniciar     V  Velocidade     Tab  Pausar     N  Avançar passo     L/P  Salvar/reproduzir     M  Métricas", 14, MUTED)
	var status := notice
	var impacts: Array = world.trace["impacts"]
	if not impacts.is_empty():
		var last: Dictionary = impacts.back()
		if not status.is_empty():
			status += "     "
		status += "Impacto %d · passo %d · terreno %d → %d" % [
			int(last["shot"]) + 1, last["tick"], last["terrain_before"], last["terrain_after"]]
	_text(Vector2(41, 689), status, 13, MUTED)

func _panel(rect: Rect2) -> void:
	draw_rect(rect, BG)
	draw_rect(rect, BORDER, false, 1.0)

func _text(at: Vector2, value: String, size: int, color: Color) -> void:
	draw_string(ThemeDB.fallback_font, at, value, HORIZONTAL_ALIGNMENT_LEFT, -1.0, size, color)
