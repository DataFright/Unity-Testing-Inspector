using System.Collections.Generic;
using UnityEngine;

namespace UTI
{
    public enum BeanColorMode
    {
        None,
        BySpeed,
        ByTime
    }

    // Reads tracker.Samples straight out of the live buffer every gizmo draw - no event
    // subscription. That buffer only exists while the GameObject is alive, and Play Mode state
    // (including it) is discarded on exiting Play Mode, so this is a live, during-Play
    // visualization, not a "review last night's run" one. Reviewing after the fact means staying
    // in Play Mode or loading a previously-written CSV back in (a stretch goal, not v1).
    /// <summary>Draws a BeanTracker's recorded path as a line in the Scene view via gizmos.</summary>
    public class BeanVisualizer : MonoBehaviour
    {
        private const float GizmoPointRadius = 0.05f;

        [SerializeField] private BeanTracker tracker;
        [SerializeField] private Color pathColor = Color.yellow;
        [SerializeField] private BeanColorMode colorMode = BeanColorMode.None;
        [SerializeField] private bool drawPoints;
        [SerializeField] private int maxPointsToDraw = 200;

        public BeanTracker Tracker { get => tracker; set => tracker = value; }
        public Color PathColor { get => pathColor; set => pathColor = value; }
        public BeanColorMode ColorMode { get => colorMode; set => colorMode = value; }
        public bool DrawPoints { get => drawPoints; set => drawPoints = value; }
        public int MaxPointsToDraw { get => maxPointsToDraw; set => maxPointsToDraw = value; }

        private void OnDrawGizmos() => DrawPath();

        private void OnDrawGizmosSelected() => DrawPath();

        private void DrawPath()
        {
            BeanTracker activeTracker = tracker != null ? tracker : GetComponent<BeanTracker>();
            if (activeTracker == null)
                return;

            IReadOnlyList<BeanSample> samples = activeTracker.Samples;
            if (samples.Count < 2)
                return;

            IReadOnlyList<int> indices = SelectIndicesToDraw(samples.Count, maxPointsToDraw);

            for (int i = 1; i < indices.Count; i++)
            {
                int fromIndex = indices[i - 1];
                int toIndex = indices[i];

                Gizmos.color = ResolveColor(toIndex, samples);
                Gizmos.DrawLine(samples[fromIndex].Position, samples[toIndex].Position);
            }

            if (drawPoints)
            {
                foreach (int index in indices)
                {
                    Gizmos.color = ResolveColor(index, samples);
                    Gizmos.DrawSphere(samples[index].Position, GizmoPointRadius);
                }
            }
        }

        /// <summary>
        /// Picks which sample indices (chronological, always including the first and last) to
        /// draw when sampleCount exceeds maxPoints - stepping through at a computed interval
        /// rather than drawing every point, so a long session's gizmo path doesn't redraw
        /// thousands of segments every editor frame. A pure function of counts (not live
        /// samples), so decimation is unit-testable without a Scene view.
        /// </summary>
        public static IReadOnlyList<int> SelectIndicesToDraw(int sampleCount, int maxPoints)
        {
            var indices = new List<int>();
            if (sampleCount <= 0)
                return indices;

            if (maxPoints <= 0 || sampleCount <= maxPoints)
            {
                for (int i = 0; i < sampleCount; i++)
                    indices.Add(i);
                return indices;
            }

            float step = (sampleCount - 1) / (float)(maxPoints - 1);
            for (int i = 0; i < maxPoints; i++)
                indices.Add(Mathf.RoundToInt(i * step));

            return indices;
        }

        /// <summary>
        /// Resolves the gizmo color for one sample under the current ColorMode. BySpeed is
        /// estimated from distance/time versus the previous sample, normalized against the
        /// fastest and slowest segment in the full (undecimated) buffer. ByTime is normalized
        /// against the buffer's first and last timestamp. Public and independent of Gizmos state,
        /// so it's testable without a Scene view.
        /// </summary>
        public Color ResolveColor(int sampleIndex, IReadOnlyList<BeanSample> samples)
        {
            switch (colorMode)
            {
                case BeanColorMode.BySpeed:
                    return Color.Lerp(Color.blue, Color.red, NormalizeSpeed(sampleIndex, samples));
                case BeanColorMode.ByTime:
                    return Color.Lerp(Color.blue, Color.red, NormalizeTime(sampleIndex, samples));
                default:
                    return pathColor;
            }
        }

        private static float NormalizeTime(int sampleIndex, IReadOnlyList<BeanSample> samples)
        {
            float first = samples[0].Timestamp;
            float last = samples[samples.Count - 1].Timestamp;
            float range = last - first;
            return range > 0f ? Mathf.Clamp01((samples[sampleIndex].Timestamp - first) / range) : 0f;
        }

        private static float NormalizeSpeed(int sampleIndex, IReadOnlyList<BeanSample> samples)
        {
            float minSpeed = float.MaxValue;
            float maxSpeed = float.MinValue;

            for (int i = 1; i < samples.Count; i++)
            {
                float speed = SpeedAt(samples, i);
                if (speed < minSpeed) minSpeed = speed;
                if (speed > maxSpeed) maxSpeed = speed;
            }

            if (minSpeed > maxSpeed)
                return 0f; // fewer than 2 samples in the buffer - no speed data to compare.

            float range = maxSpeed - minSpeed;
            if (range <= 0f)
                return 0f;

            return Mathf.Clamp01((SpeedAt(samples, sampleIndex) - minSpeed) / range);
        }

        private static float SpeedAt(IReadOnlyList<BeanSample> samples, int index)
        {
            if (index <= 0)
                return 0f;

            BeanSample previous = samples[index - 1];
            BeanSample current = samples[index];
            float dt = current.Timestamp - previous.Timestamp;
            return dt > 0f ? Vector3.Distance(previous.Position, current.Position) / dt : 0f;
        }
    }
}
