using Artillery.Core;
using Artillery.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Artillery.Tests
{
    public sealed class GameplayTests
    {
        [Test]
        public void PausedInputBecomesOneTickAndResetClearsQueuedActions()
        {
            using var session = new ArenaSession(Scenario.Sandbox);
            session.Paused = true;
            session.QueueFire();
            session.Advance(0.5, 1, 1, 0);
            Assert.That(session.State.Tick, Is.Zero);

            session.StepOnce(1, -1, 0);
            Assert.That(session.State.Tick, Is.EqualTo(1));
            Assert.That(session.State.GetTank(0).Angle, Is.EqualTo(39));
            Assert.That(session.State.Power, Is.EqualTo(73));
            Assert.That(session.State.ShotCount, Is.EqualTo(1));
            Assert.That(session.CaptureReplay().commands.Count, Is.EqualTo(1));

            session.QueueFire();
            session.QueueDrill(new Vector2(20, 6), new Vector2(21, 6));
            session.Reset(Scenario.Sandbox);
            Assert.That(session.Paused, Is.False);
            session.Advance(FixedClock.Interval, 0, 0, 0);
            Assert.That(session.State.ShotCount, Is.Zero);
            Assert.That(session.CaptureReplay().commands, Is.Empty);
        }

        [Test]
        public void RecordedVolleyReplaysAtAnotherFrameCadence()
        {
            using var original = new ArenaSession(Scenario.Volley);
            for (int frame = 0; frame < 90; frame++)
                original.Advance(1.0 / 30, 0, 0, 0);
            RunTrace recording = JsonUtility.FromJson<RunTrace>(JsonUtility.ToJson(original.CaptureReplay()));
            Assert.That(original.State.PeakShots, Is.EqualTo(4));
            Assert.That(recording.commands, Has.Count.EqualTo(1));
            Assert.That(recording.commands[0].tick, Is.EqualTo(Experiment.VolleyTick));
            Assert.That(recording.impacts, Is.Not.Empty);

            using var replay = new ArenaSession(Scenario.Sandbox);
            Assert.That(replay.BeginReplay(recording), Is.True);
            for (int frame = 0; frame < 432; frame++)
                replay.Advance(1.0 / 144, 1, 1, -1);
            Assert.That(replay.State.Tick, Is.EqualTo(180));
            Assert.That(replay.ReplayFinished, Is.True);
            Assert.That(replay.Paused, Is.True);
            Assert.That(replay.FirstDivergentTick, Is.Null);
            Assert.That(replay.ReplayImpactsMatch, Is.True);
        }

        [Test]
        public void ReplayRejectsInvalidMetadataAndReportsFirstMismatchedStep()
        {
            using var session = new ArenaSession(Scenario.Sandbox);
            session.Advance(FixedClock.Interval * 2, 0, 0, 0);
            RunTrace recording = session.CaptureReplay();
            recording.scenario = int.MaxValue;
            Assert.That(session.BeginReplay(recording), Is.False);
            Assert.That(session.State.Tick, Is.EqualTo(2));

            recording.scenario = (int)Scenario.Sandbox;
            recording.hashes[0] = recording.hashes[0] == "0000000000000000" ?
                "ffffffffffffffff" : "0000000000000000";
            Assert.That(session.BeginReplay(recording), Is.True);
            session.Advance(FixedClock.Interval * 3, 0, 0, 0);
            Assert.That(session.ReplayFinished, Is.True);
            Assert.That(session.State.Tick, Is.EqualTo(2));
            Assert.That(session.FirstDivergentTick, Is.EqualTo(0));
        }
    }
}
