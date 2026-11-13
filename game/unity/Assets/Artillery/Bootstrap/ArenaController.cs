using Artillery.Core;
using Artillery.Gameplay;
using Artillery.Presentation;
using UnityEngine;

namespace Artillery
{
    public sealed class ArenaController : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        private ArenaSession session;
        private ArenaPresentation presentation;
        private IArenaViewState displayed;
        private Vector2? drillAnchor;
        private float previousFixedStep;

        public IArenaViewState State => session.State;

        private void Awake()
        {
            previousFixedStep = Time.fixedDeltaTime;
            Time.fixedDeltaTime = Experiment.Step;
            if (arenaCamera == null)
            {
                var cameraObject = new GameObject("Arena Camera");
                cameraObject.transform.SetParent(transform, false);
                arenaCamera = cameraObject.AddComponent<Camera>();
            }

            arenaCamera.transform.SetPositionAndRotation(new Vector3(20, 8.35f, -24), Quaternion.identity);
            arenaCamera.orthographic = true;
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.nearClipPlane = 0.1f;
            arenaCamera.farClipPlane = 60f;
            UpdateFraming();

            session = new ArenaSession(Scenario.Sandbox);
            presentation = new ArenaPresentation(transform, arenaCamera);
            SyncView();
            RefreshView();
        }

        private void FixedUpdate()
        {
            session.Advance(FixedClock.Interval, Axis(KeyCode.UpArrow, KeyCode.DownArrow),
                Axis(KeyCode.E, KeyCode.Q), Axis(KeyCode.D, KeyCode.A));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) ResetArena(Scenario.Sandbox);
            if (Input.GetKeyDown(KeyCode.B)) ResetArena(Scenario.Volley);
            if (Input.GetKeyDown(KeyCode.V)) session.Slow = !session.Slow;
            if (Input.GetKeyDown(KeyCode.Tab)) session.Paused = !session.Paused;
            if (Input.GetKeyDown(KeyCode.F2)) presentation.CycleQuality();

            if (Input.GetKeyDown(KeyCode.P) && session.LoadReplay())
                drillAnchor = null;
            if (Input.GetKeyDown(KeyCode.L)) session.SaveReplay();
            if (Input.GetKeyDown(KeyCode.Space)) session.QueueFire();
            if (Input.GetKeyDown(KeyCode.Escape)) drillAnchor = null;
            if (Input.GetMouseButtonDown(1) && !session.IsReplaying)
            {
                Vector2? point = Pointer();
                if (point.HasValue) MarkCut(point.Value);
            }
            if (Input.GetKeyDown(KeyCode.N) && session.Paused)
                session.StepOnce(Axis(KeyCode.UpArrow, KeyCode.DownArrow), Axis(KeyCode.E, KeyCode.Q),
                    Axis(KeyCode.D, KeyCode.A));
        }

        private void LateUpdate()
        {
            UpdateFraming();
            SyncView();
            RefreshView();
        }

        public void ResetArena(Scenario scenario)
        {
            drillAnchor = null;
            session.Reset(scenario);
        }

        private void SyncView()
        {
            if (ReferenceEquals(displayed, State)) return;
            displayed = State;
            presentation.Attach(displayed);
        }

        private void RefreshView()
        {
            presentation.Refresh(new PresentationStatus(session.Paused, session.Slow, session.Notice,
                session.IsReplaying ? null : Pointer(), drillAnchor));
        }

        private void UpdateFraming()
        {
            arenaCamera.orthographicSize = Mathf.Max(12f, 21f / Mathf.Max(0.1f, arenaCamera.aspect));
        }

        private Vector2? Pointer()
        {
            Vector3 mouse = Input.mousePosition;
            if (!arenaCamera.pixelRect.Contains(mouse)) return null;
            Vector3 point = arenaCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 24));
            if (point.x < 0 || point.x >= Experiment.Width || point.y < 0 || point.y >= Experiment.Height)
                return null;
            return (Vector2)point;
        }

        private void MarkCut(Vector2 point)
        {
            if (drillAnchor.HasValue)
            {
                session.QueueDrill(drillAnchor.Value, point);
                drillAnchor = null;
            }
            else
                drillAnchor = point;
        }

        private static int Axis(KeyCode positive, KeyCode negative) =>
            (Input.GetKey(positive) ? 1 : 0) - (Input.GetKey(negative) ? 1 : 0);

        private void OnGUI() => presentation?.DrawHud();

        private void OnDestroy()
        {
            presentation?.Dispose();
            session?.Dispose();
            Time.fixedDeltaTime = previousFixedStep;
        }
    }
}
