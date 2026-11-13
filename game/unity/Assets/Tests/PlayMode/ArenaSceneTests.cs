using System.Collections;
using Artillery.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Artillery.Tests
{
    public sealed class ArenaSceneTests
    {
        [UnityTest]
        public IEnumerator BuildSceneBootsAndRunsTheVolley()
        {
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Additive);
            Scene arena = SceneManager.GetSceneByName("Arena");
            Assert.That(arena.isLoaded, Is.True);
            ArenaController controller = null;
            foreach (GameObject root in arena.GetRootGameObjects())
            {
                controller = root.GetComponent<ArenaController>();
                if (controller != null) break;
            }

            try
            {
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.GetComponentInChildren<Camera>().orthographic, Is.True);
                Assert.That(controller.State.Scenario, Is.EqualTo(Scenario.Sandbox));
                Assert.That(controller.State.TankCount, Is.EqualTo(2));

                controller.ResetArena(Scenario.Volley);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (controller.State.Tick < Experiment.VolleyTick + 90 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(controller.State.Tick, Is.GreaterThanOrEqualTo(Experiment.VolleyTick + 90));
                Assert.That(controller.State.TankCount, Is.EqualTo(4));
                Assert.That(controller.State.PeakShots, Is.EqualTo(4));
                Assert.That(controller.State.ImpactCount, Is.GreaterThan(0));
                Assert.That(controller.State.TerrainVersion, Is.GreaterThan(0));

                controller.ResetArena(Scenario.Sandbox);
                Assert.That(controller.State.Tick, Is.Zero);
                Assert.That(controller.State.PeakShots, Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (arena.isLoaded) SceneManager.UnloadSceneAsync(arena);
            }
            while (arena.isLoaded)
                yield return null;
        }
    }
}
