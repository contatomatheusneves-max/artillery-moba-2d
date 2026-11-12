using System;
using System.IO;
using Artillery.G2.Core;
using UnityEngine;

namespace Artillery.G2
{
    public sealed class PocDriver : MonoBehaviour
    {
        private readonly FixedClock clock = new FixedClock();
        private PocWorld world;
        private ArenaView view;
        private Camera arenaCamera;
        private RunTrace replay;
        private int replayIndex, divergence;
        private bool paused, slow, fire;
        private int angle, power, drive;
        private Vector2? anchor;
        private TickCommand? pendingDrill;
        private string notice;
        private GUIStyle title, body, dim, value;

        private static string TracePath => Path.Combine(Application.persistentDataPath, "g2-01-replay.json");

        private void Awake()
        {
            var cameraObject = new GameObject("Arena camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(20, 8.35f, -20);
            arenaCamera = cameraObject.AddComponent<Camera>();
            arenaCamera.orthographic = true;
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.nearClipPlane = 0.1f;
            arenaCamera.farClipPlane = 50f;
            StartRun(Scenario.Sandbox);
        }

        private void StartRun(Scenario scenario)
        {
            view?.Dispose();
            world?.Dispose();
            world = new PocWorld(scenario);
            view = new ArenaView(world, transform, arenaCamera);
            clock.Reset();
            anchor = null;
            pendingDrill = null;
            fire = false;
            paused = false;
            angle = world.Tanks[0].Angle;
            power = world.Power;
            drive = world.Drive;
            replay = null;
            replayIndex = 0;
            divergence = -1;
            notice = scenario == Scenario.Volley ? "Salva programada para o passo 45." : "";
            view.Refresh(null, null);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) StartRun(Scenario.Sandbox);
            if (Input.GetKeyDown(KeyCode.B)) StartRun(Scenario.Volley);
            if (Input.GetKeyDown(KeyCode.P)) LoadReplay();
            if (Input.GetKeyDown(KeyCode.L)) SaveReplay();
            if (Input.GetKeyDown(KeyCode.V)) slow = !slow;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                paused = !paused;
                clock.Reset();
            }

            Vector2? pointer = Pointer();
            if (replay == null)
            {
                fire |= Input.GetKeyDown(KeyCode.Space);
                if (Input.GetKeyDown(KeyCode.Escape)) anchor = null;
                if (Input.GetMouseButtonDown(1) && pointer.HasValue)
                    SetDrillPoint(pointer.Value);
            }

            if (paused)
            {
                if (Input.GetKeyDown(KeyCode.N)) TickOnce();
            }
            else
                clock.Advance(Time.unscaledDeltaTime * (slow ? 0.25 : 1), StepIfRunning, 8);
            view.Refresh(replay == null ? pointer : null, anchor);
        }

        private Vector2? Pointer()
        {
            Vector3 mouse = Input.mousePosition;
            if (mouse.x < 0 || mouse.x >= Screen.width || mouse.y < 0 || mouse.y >= Screen.height)
                return null;
            Vector3 point = arenaCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 20));
            return point.x >= 0 && point.x < Experiment.Width && point.y >= 0 && point.y < Experiment.Height
                ? (Vector2)point : (Vector2?)null;
        }

        private void SetDrillPoint(Vector2 point)
        {
            if (!anchor.HasValue)
            {
                anchor = point;
                return;
            }
            Vector2 from = anchor.Value;
            pendingDrill = new TickCommand
            {
                drill = true,
                fromX = Coordinate(from.x), fromY = Coordinate(from.y),
                toX = Coordinate(point.x), toY = Coordinate(point.y)
            };
            anchor = null;
        }

        private static int Coordinate(float value) => Mathf.RoundToInt(value * Experiment.CoordinateScale);

        private void StepIfRunning()
        {
            if (!paused) TickOnce();
        }

        private void TickOnce()
        {
            if (replay != null)
            {
                StepReplay();
                return;
            }
            angle = Mathf.Clamp(angle + Axis(KeyCode.UpArrow, KeyCode.DownArrow), 0, 180);
            power = Mathf.Clamp(power + Axis(KeyCode.E, KeyCode.Q), 0, 100);
            drive = Axis(KeyCode.D, KeyCode.A);
            bool volley = world.Scenario == Scenario.Volley && world.Tick == Experiment.VolleyTick;
            bool shoot = fire && (world.Scenario != Scenario.Volley || world.Tick > Experiment.VolleyTick);
            TickCommand command = pendingDrill.GetValueOrDefault();
            command.tick = world.Tick;
            command.angle = angle;
            command.power = power;
            command.drive = drive;
            command.fire = shoot;
            command.volley = volley;

            if (shoot && power == 0) notice = "Aumente a potência para disparar.";
            bool changed = volley || shoot || command.drill || angle != world.Tanks[0].Angle ||
                power != world.Power || drive != world.Drive;
            world.Step(changed ? command : (TickCommand?)null);
            if (volley)
            {
                angle = world.Tanks[0].Angle;
                power = world.Power;
                notice = "Quatro projéteis lançados no mesmo passo.";
            }
            pendingDrill = null;
            fire = false;
        }

        private static int Axis(KeyCode positive, KeyCode negative) =>
            (Input.GetKey(positive) ? 1 : 0) - (Input.GetKey(negative) ? 1 : 0);

        private void SaveReplay()
        {
            try
            {
                if (world.Tick == 0) { notice = "Avance pelo menos um passo para salvar."; return; }
                File.WriteAllText(TracePath, JsonUtility.ToJson(world.Trace, true));
                notice = "Replay salvo. Use P para reproduzir.";
                Debug.Log("G2-01 replay: " + TracePath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                notice = "Falha ao salvar o replay. Veja o Console.";
                Debug.LogWarning(error.Message);
            }
        }

        private void LoadReplay()
        {
            try
            {
                var source = JsonUtility.FromJson<RunTrace>(File.ReadAllText(TracePath));
                if (!ValidTrace(source))
                {
                    notice = "Replay incompatível ou incompleto.";
                    return;
                }
                StartRun((Scenario)source.scenario);
                replay = source;
                notice = source.unity == Application.unityVersion && source.platform == Application.platform.ToString()
                    ? "Reproduzindo comandos gravados." : "Reproduzindo em outro ambiente de execução.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                notice = "Falha ao abrir o replay. Veja o Console.";
                Debug.LogWarning(error.Message);
            }
        }

        private static bool ValidTrace(RunTrace trace)
        {
            if (trace == null || trace.experiment != Experiment.Version ||
                !Enum.IsDefined(typeof(Scenario), trace.scenario) || trace.hashes == null ||
                trace.hashes.Count == 0 || trace.commands == null) return false;
            int last = -1;
            foreach (TickCommand command in trace.commands)
            {
                if (command.tick <= last || command.tick >= trace.hashes.Count) return false;
                last = command.tick;
            }
            return true;
        }

        private void StepReplay()
        {
            int tick = world.Tick;
            if (tick >= replay.hashes.Count) { paused = true; return; }
            TickCommand? command = null;
            if (replayIndex < replay.commands.Count && replay.commands[replayIndex].tick == tick)
                command = replay.commands[replayIndex++];
            world.Step(command);
            if (divergence < 0 && world.Trace.hashes[tick] != replay.hashes[tick])
                divergence = tick;
            if (world.Tick < replay.hashes.Count) return;
            paused = true;
            notice = divergence < 0 ? "Replay concluído. Todos os hashes coincidiram."
                : "Replay divergiu no passo " + divergence + ".";
        }

        private void OnGUI()
        {
            if (world == null || Event.current.type != EventType.Repaint) return;
            if (title == null)
            {
                title = Style(22, 0xe9f2ee, FontStyle.Bold);
                body = Style(14, 0xe5eceb);
                dim = Style(12, 0xb5c8cc);
                value = Style(15, 0x83e7d1, FontStyle.Bold);
            }
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 620f), 0.45f, 1.5f);
            float width = Screen.width / scale, height = Screen.height / scale;
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Panel(20, 20, 316, 108);
            Label(34, 31, "Artilharia 2D", title);
            Label(35, 61, world.Scenario == Scenario.Volley ? "Cenário de salva" : "Campo de testes", dim);
            Label(35, 87, $"Mira {world.Tanks[0].Angle:000}°     Potência {world.Power:000}/100", value);

            Panel(width - 374, 20, 354, 108);
            Label(width - 360, 31, $"Passo {world.Tick:0000}   ·   {(paused ? "Pausado" : slow ? "0,25×" : "1×")}", value);
            Label(width - 360, 56, $"Projéteis {world.Weapon.Shots.Count}/4   ·   Pico {world.PeakShots}   ·   Terreno v{world.Terrain.Grid.Version}", body);
            bool contact = world.Tanks[0].Hull.IsTouching(world.Terrain.Collider);
            Label(width - 360, 81, $"Tanque {(contact ? "em contato" : "sem contato")}   ·   Células {world.Terrain.Grid.SolidCount}", dim);

            Panel(20, height - 103, width - 40, 83);
            Label(34, height - 96, "A/D  Mover     ↑/↓  Mirar     Q/E  Potência     Espaço  Disparar     Botão direito ×2  Escavar", body);
            Label(34, height - 72, "B  Salva     R  Reiniciar     V  Velocidade     Tab  Pausa     N  Avançar passo     L/P  Salvar/reproduzir", dim);
            string status = anchor.HasValue ? "Marque o outro extremo do túnel. Esc cancela."
                : notice;
            if (world.Trace.impacts.Count > 0)
            {
                ImpactRecord last = world.Trace.impacts[world.Trace.impacts.Count - 1];
                status += $"   Último impacto {last.shot + 1} · passo {last.tick} · terreno {last.terrainBefore} → {last.terrainAfter}";
            }
            Label(34, height - 48, status, dim);
            GUI.matrix = saved;
        }

        private static GUIStyle Style(int size, int color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
            style.normal.textColor = ArenaView.Hex(color);
            return style;
        }

        private static void Label(float x, float y, string message, GUIStyle style) =>
            GUI.Label(new Rect(x, y, 950, 27), message, style);

        private static void Panel(float x, float y, float width, float height)
        {
            Color saved = GUI.color;
            GUI.color = new Color(0.043f, 0.088f, 0.124f, 0.94f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private void OnDestroy()
        {
            view?.Dispose();
            world?.Dispose();
        }
    }
}
