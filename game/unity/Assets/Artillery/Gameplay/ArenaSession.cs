using System;
using System.IO;
using Artillery.Core;
using UnityEngine;

namespace Artillery.Gameplay
{
    public sealed class ArenaSession : IDisposable
    {
        private const int FrameStepBudget = 8;
        private readonly FixedClock clock = new FixedClock();
        private readonly Action runFrameStep;
        private ArenaSimulation world;
        private RunTrace playback;
        private int replayCommand, frameAim, framePower, frameDrive;
        private bool paused;
        private bool fireRequested;
        private TickCommand? pendingDrill;
        private bool replayEnvironmentDiffers;

        public ArenaSession(Scenario scenario)
        {
            runFrameStep = RunFrameStep;
            Reset(scenario);
        }

        public static string ReplayPath => Path.Combine(Application.persistentDataPath, "artillery-v01-replay.json");
        public IArenaViewState State => world;
        public string Notice { get; private set; }
        public bool IsReplaying => playback != null;
        public bool ReplayFinished { get; private set; }
        public int? FirstDivergentTick { get; private set; }
        public bool? ReplayImpactsMatch { get; private set; }
        public bool Slow { get; set; }

        public bool Paused
        {
            get => paused;
            set
            {
                if (paused == value || (!value && ReplayFinished)) return;
                paused = value;
                clock.Reset();
            }
        }

        public void Reset(Scenario scenario)
        {
            if (!Enum.IsDefined(typeof(Scenario), scenario))
                throw new ArgumentOutOfRangeException(nameof(scenario));
            var next = new ArenaSimulation(scenario);
            world?.Dispose();
            world = next;
            playback = null;
            replayCommand = 0;
            replayEnvironmentDiffers = false;
            ReplayFinished = false;
            FirstDivergentTick = null;
            ReplayImpactsMatch = null;
            paused = false;
            fireRequested = false;
            pendingDrill = null;
            clock.Reset();
            Notice = scenario == Scenario.Volley ? $"Salva programada para o passo {Experiment.VolleyTick}." : "";
        }

        public void Advance(double seconds, int aimAxis, int powerAxis, int driveAxis)
        {
            if (Paused) return;
            frameAim = aimAxis;
            framePower = powerAxis;
            frameDrive = driveAxis;
            clock.Advance(seconds * (Slow ? 0.25 : 1.0), runFrameStep, FrameStepBudget);
        }

        private void RunFrameStep()
        {
            if (!Paused) Tick(frameAim, framePower, frameDrive);
        }

        public void StepOnce(int aimAxis, int powerAxis, int driveAxis)
        {
            if (Paused && !ReplayFinished) Tick(aimAxis, powerAxis, driveAxis);
        }

        public void QueueFire()
        {
            if (!IsReplaying) fireRequested = true;
        }

        public void QueueDrill(Vector2 from, Vector2 to)
        {
            if (IsReplaying || pendingDrill.HasValue || !InsideArena(from) || !InsideArena(to)) return;
            pendingDrill = new TickCommand
            {
                drill = true,
                fromX = Coordinate(from.x), fromY = Coordinate(from.y),
                toX = Coordinate(to.x), toY = Coordinate(to.y)
            };
        }

        private static bool InsideArena(Vector2 point) =>
            point.x >= 0 && point.x <= Experiment.Width && point.y >= 0 && point.y <= Experiment.Height;

        private static int Coordinate(float value) => Mathf.RoundToInt(value * Experiment.CoordinateScale);

        private void Tick(int aimAxis, int powerAxis, int driveAxis)
        {
            if (IsReplaying)
            {
                ReplayTick();
                return;
            }

            int angle = State.GetTank(0).Angle;
            TickCommand command = pendingDrill.GetValueOrDefault();
            command.tick = State.Tick;
            command.angle = Mathf.Clamp(angle + Math.Sign(aimAxis), 0, 180);
            command.power = Mathf.Clamp(State.Power + Math.Sign(powerAxis), 0, 100);
            command.drive = Math.Sign(driveAxis);
            bool volley = State.Scenario == Scenario.Volley;
            command.volley = volley && State.Tick == Experiment.VolleyTick;
            command.fire = fireRequested && (!volley || State.Tick > Experiment.VolleyTick);

            bool changed = command.volley || command.fire || command.drill ||
                command.angle != angle || command.power != State.Power || command.drive != world.Drive;
            world.Step(changed ? command : (TickCommand?)null);
            if (command.volley)
                Notice = "Quatro projéteis lançados no mesmo passo.";
            else if (command.fire && command.power == 0)
                Notice = "Aumente a potência para disparar.";
            else if (command.fire || command.drill || Notice == "Aumente a potência para disparar." && command.power > 0)
                Notice = "";

            fireRequested = false;
            pendingDrill = null;
        }

        public RunTrace CaptureReplay() => ReplayStorage.Copy(world.Trace);

        public bool BeginReplay(RunTrace recording)
        {
            if (!ReplayStorage.IsValid(recording))
            {
                Notice = "Replay incompatível ou incompleto.";
                return false;
            }

            var source = ReplayStorage.Copy(recording);
            Reset((Scenario)source.scenario);
            playback = source;
            replayEnvironmentDiffers = source.unity != Application.unityVersion ||
                source.platform != Application.platform.ToString();
            Notice = replayEnvironmentDiffers ? "Reproduzindo em outro ambiente de execução." :
                "Reproduzindo comandos gravados.";
            return true;
        }

        private void ReplayTick()
        {
            int tick = State.Tick;
            TickCommand? command = null;
            if (replayCommand < playback.commands.Count && playback.commands[replayCommand].tick == tick)
                command = playback.commands[replayCommand++];
            world.Step(command);
            if (!FirstDivergentTick.HasValue && world.Trace.hashes[tick] != playback.hashes[tick])
                FirstDivergentTick = tick;
            if (State.Tick < playback.hashes.Count) return;

            ReplayImpactsMatch = ReplayStorage.ImpactsMatch(world.Trace, playback);
            Paused = true;
            ReplayFinished = true;
            Notice = FirstDivergentTick.HasValue ? $"Replay divergiu no passo {FirstDivergentTick.Value}." :
                ReplayImpactsMatch.Value ? "Replay concluído. Hashes e impactos coincidiram." :
                "Hashes coincidiram. O registro de impactos divergiu.";
            if (replayEnvironmentDiffers)
                Notice += " Ambiente diferente.";
        }

        public bool SaveReplay()
        {
            if (State.Tick == 0)
            {
                Notice = "Avance pelo menos um passo para salvar.";
                return false;
            }
            if (IsReplaying)
            {
                Notice = "O arquivo original está aberto em reprodução.";
                return false;
            }

            try
            {
                ReplayStorage.Save(ReplayPath, world.Trace);
                Notice = "Replay salvo. Use P para reproduzir.";
                Debug.Log("Replay: " + ReplayPath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Notice = "Falha ao salvar o replay. Veja o Console.";
                Debug.LogWarning(error.Message);
                return false;
            }
        }

        public bool LoadReplay()
        {
            try
            {
                return BeginReplay(ReplayStorage.Load(ReplayPath));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                Notice = "Falha ao abrir o replay. Veja o Console.";
                Debug.LogWarning(error.Message);
                return false;
            }
        }

        public void Dispose()
        {
            world?.Dispose();
            world = null;
        }
    }
}
