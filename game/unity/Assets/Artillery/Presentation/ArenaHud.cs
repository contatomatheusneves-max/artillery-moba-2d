using Artillery.Core;
using UnityEngine;

namespace Artillery.Presentation
{
    internal sealed class ArenaHud
    {
        private static readonly Color Ink = ArenaArtwork.Hex(0x0d1d27);
        private static readonly Color Border = ArenaArtwork.Hex(0x456168);
        private static readonly Color Muted = ArenaArtwork.Hex(0xaac0bf);
        private static readonly Color Teal = ArenaArtwork.Teal;
        private static readonly Color Gold = ArenaArtwork.Amber;
        private static readonly Color White = ArenaArtwork.Pale;
        private GUIStyle title, label, muted, number, small;
        private int lastTick = -1;
        private int lastImpactCount = -1;
        private IArenaViewState lastState;
        private string tickReadout, shotReadout, lastImpact, terrainReadout, contactReadout, aimReadout;
        private bool cachedGrounded;

        public void Draw(Camera camera, IArenaViewState state, PresentationStatus status, string quality)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            ReadState(state);

            float scale = Mathf.Max(0.22f, Mathf.Min(Screen.width / 1120f, Screen.height / 620f, 1.75f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            Matrix4x4 original = GUI.matrix;
            Color originalColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.color = Color.white;

            float right = width - 278;
            Panel(new Rect(18, 18, 277, 102), Teal);
            Label(new Rect(32, 27, 241, 32), "Artilharia 2D", title);
            Label(new Rect(32, 60, 240, 18),
                state.Scenario == Scenario.Volley ? "Arena de salva" : "Campo de testes", muted);
            Label(new Rect(32, 80, 68, 21), aimReadout, number);
            Label(new Rect(103, 83, 163, 20), "Potência  " + state.Power.ToString("D3") + " / 100", label);
            Fill(new Rect(104, 104, 168, 3), Border);
            Fill(new Rect(104, 104, state.Power * 1.68f, 3), Gold);

            Panel(new Rect(right, 18, 260, 102), Gold);
            Label(new Rect(right + 14, 28, 170, 22), tickReadout, number);
            string pace = status.Paused ? "Pausado" : status.Slow ? "0,25×" : "1×";
            Label(new Rect(right + 171, 32, 74, 19), pace, muted);
            Label(new Rect(right + 14, 55, 235, 20), shotReadout, label);
            Label(new Rect(right + 14, 78, 240, 20), terrainReadout, muted);
            Label(new Rect(right + 14, 97, 240, 18), contactReadout, small);

            float footer = height - 90;
            Panel(new Rect(18, footer, width - 36, 72), Border);
            Label(new Rect(32, footer + 7, width - 64, 19),
                "A/D  Mover    ↑/↓  Mirar    Q/E  Potência    Espaço  Disparar    Botão direito ×2  Escavar    Esc  Cancelar escavação", label);
            Label(new Rect(32, footer + 26, width - 64, 19),
                "B  Salva    R  Reiniciar    V  Velocidade    Tab  Pausar    N  Avançar passo    L/P  Gravar/reproduzir    F2  Qualidade", muted);
            string notice = status.Notice;
            if (string.IsNullOrEmpty(notice)) notice = lastImpact;
            Label(new Rect(32, footer + 47, width - 337, 18), notice ?? "", small);
            Label(new Rect(width - 299, footer + 47, 268, 18), "Câmera fixa   ·   Visual " + quality, small);

            DrawTankMarker(camera, state.GetTank(0).Position, scale);
            GUI.matrix = original;
            GUI.color = originalColor;
        }

        private void ReadState(IArenaViewState state)
        {
            if (ReferenceEquals(state, lastState) && state.Tick == lastTick) return;
            if (!ReferenceEquals(state, lastState)) lastImpactCount = -1;
            lastState = state;
            lastTick = state.Tick;
            TankPose player = state.GetTank(0);
            cachedGrounded = player.Grounded;
            tickReadout = "Passo " + state.Tick.ToString("D4");
            shotReadout = $"Projéteis {state.ShotCount}/{Experiment.MaxShots}   ·   Pico {state.PeakShots}";
            terrainReadout = $"Terreno v{state.TerrainVersion}   ·   {state.SolidCells} células";
            contactReadout = cachedGrounded ? "Tanque em contato com o solo" : "Tanque sem contato com o solo";
            aimReadout = player.Angle.ToString("D3") + "°";
            if (state.ImpactCount == lastImpactCount) return;
            lastImpactCount = state.ImpactCount;
            lastImpact = "";
            if (lastImpactCount > 0)
            {
                ImpactRecord impact = state.GetImpact(lastImpactCount - 1);
                lastImpact = $"Último impacto {impact.shot + 1}   ·   passo {impact.tick}   ·   terreno {impact.terrainBefore} → {impact.terrainAfter}";
            }
        }

        private static void DrawTankMarker(Camera camera, Vector2 tank, float scale)
        {
            Vector3 screen = camera.WorldToScreenPoint(new Vector3(tank.x, tank.y + 0.72f, -0.9f));
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height)
                return;
            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;
            Fill(new Rect(x - 6, y, 12, 2), Teal);
            Fill(new Rect(x - 3, y + 2, 6, 2), Teal);
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = Style(24, White, FontStyle.Bold);
            label = Style(14, White);
            muted = Style(13, Muted);
            number = Style(17, Teal, FontStyle.Bold);
            small = Style(12, Muted);
        }

        private static GUIStyle Style(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = fontStyle, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft
            };
            style.normal.textColor = color;
            return style;
        }

        private static void Label(Rect rect, string text, GUIStyle style) => GUI.Label(rect, text, style);

        private static void Panel(Rect rect, Color accent)
        {
            Fill(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), ArenaArtwork.Fade(Color.black, 0.22f));
            Fill(rect, ArenaArtwork.Fade(Ink, 0.94f));
            Fill(new Rect(rect.x, rect.y, rect.width, 1), Border);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), accent);
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
