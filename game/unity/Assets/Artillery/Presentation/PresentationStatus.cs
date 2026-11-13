using UnityEngine;

namespace Artillery.Presentation
{
    public readonly struct PresentationStatus
    {
        public readonly bool Paused, Slow;
        public readonly string Notice;
        public readonly Vector2? DrillPointer, DrillAnchor;

        public PresentationStatus(bool paused, bool slow, string notice, Vector2? drillPointer, Vector2? drillAnchor)
        {
            Paused = paused;
            Slow = slow;
            Notice = notice;
            DrillPointer = drillPointer;
            DrillAnchor = drillAnchor;
        }
    }
}
