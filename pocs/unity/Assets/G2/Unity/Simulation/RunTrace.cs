using System;
using System.Collections.Generic;

namespace Artillery.G2
{
    [Serializable]
    public struct TickCommand
    {
        public int tick, angle, power, drive;
        public bool fire, drill, volley;
        public int fromX, fromY, toX, toY;
    }

    [Serializable]
    public struct ImpactRecord
    {
        public int tick, shot, target, terrainBefore, terrainAfter;
        public float fraction, x, y;
    }

    [Serializable]
    public sealed class RunTrace
    {
        public string experiment, unity, platform;
        public int scenario;
        public List<TickCommand> commands = new List<TickCommand>();
        public List<string> hashes = new List<string>();
        public List<ImpactRecord> impacts = new List<ImpactRecord>();
    }
}
