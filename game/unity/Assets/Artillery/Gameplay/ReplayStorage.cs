using System;
using System.Collections.Generic;
using System.IO;
using Artillery.Core;
using UnityEngine;

namespace Artillery.Gameplay
{
    internal static class ReplayStorage
    {
        internal static RunTrace Copy(RunTrace source) => new RunTrace
        {
            experiment = source.experiment,
            unity = source.unity,
            platform = source.platform,
            scenario = source.scenario,
            commands = new List<TickCommand>(source.commands),
            hashes = new List<string>(source.hashes),
            impacts = new List<ImpactRecord>(source.impacts)
        };

        internal static bool IsValid(RunTrace trace)
        {
            if (trace == null || trace.experiment != Experiment.Version ||
                string.IsNullOrEmpty(trace.unity) || string.IsNullOrEmpty(trace.platform) ||
                !Enum.IsDefined(typeof(Scenario), trace.scenario) ||
                trace.hashes == null || trace.hashes.Count == 0 || trace.commands == null || trace.impacts == null)
                return false;

            foreach (string hash in trace.hashes)
            {
                if (hash == null || hash.Length != 16) return false;
                foreach (char digit in hash)
                    if (!((digit >= '0' && digit <= '9') || (digit >= 'a' && digit <= 'f'))) return false;
            }

            int previousTick = -1;
            foreach (TickCommand command in trace.commands)
            {
                if (command.tick <= previousTick || command.tick >= trace.hashes.Count ||
                    command.angle < 0 || command.angle > 180 || command.power < 0 || command.power > 100 ||
                    command.drive < -1 || command.drive > 1)
                    return false;
                previousTick = command.tick;
            }

            previousTick = -1;
            foreach (ImpactRecord impact in trace.impacts)
            {
                if (impact.tick < 0 || impact.tick < previousTick || impact.tick >= trace.hashes.Count ||
                    impact.shot < 0 || impact.target < -1 || impact.terrainBefore < 0 ||
                    impact.terrainAfter < impact.terrainBefore || !Finite(impact.fraction) ||
                    impact.fraction < 0 || impact.fraction > 1 || !Finite(impact.x) || !Finite(impact.y))
                    return false;
                previousTick = impact.tick;
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool ImpactsMatch(RunTrace actual, RunTrace expected)
        {
            if (actual.impacts.Count != expected.impacts.Count) return false;
            for (int i = 0; i < actual.impacts.Count; i++)
            {
                ImpactRecord a = actual.impacts[i];
                ImpactRecord b = expected.impacts[i];
                if (a.tick != b.tick || a.shot != b.shot || a.target != b.target ||
                    a.terrainBefore != b.terrainBefore || a.terrainAfter != b.terrainAfter ||
                    a.fraction != b.fraction || a.x != b.x || a.y != b.y)
                    return false;
            }
            return true;
        }

        internal static RunTrace Load(string path) => JsonUtility.FromJson<RunTrace>(File.ReadAllText(path));

        internal static void Save(string path, RunTrace trace)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(trace, true));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
