using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanVisualizerTests
    {
        // Fake IGizmoDrawer that records exactly what DrawPath() sent it - the color active at
        // the moment of each DrawLine/DrawSphere call - so DrawPath()'s actual draw-call
        // sequence can be asserted without a live Scene view. See BeanVisualizer.IGizmoDrawer.
        private sealed class RecordingGizmoDrawer : IGizmoDrawer
        {
            public readonly struct LineCall
            {
                public LineCall(Vector3 from, Vector3 to, Color color)
                {
                    From = from;
                    To = to;
                    Color = color;
                }

                public Vector3 From { get; }
                public Vector3 To { get; }
                public Color Color { get; }
            }

            public readonly struct SphereCall
            {
                public SphereCall(Vector3 center, float radius, Color color)
                {
                    Center = center;
                    Radius = radius;
                    Color = color;
                }

                public Vector3 Center { get; }
                public float Radius { get; }
                public Color Color { get; }
            }

            private Color currentColor;

            public Color Color { set => currentColor = value; }
            public List<LineCall> Lines { get; } = new List<LineCall>();
            public List<SphereCall> Spheres { get; } = new List<SphereCall>();

            public void DrawLine(Vector3 from, Vector3 to) => Lines.Add(new LineCall(from, to, currentColor));
            public void DrawSphere(Vector3 center, float radius) => Spheres.Add(new SphereCall(center, radius, currentColor));
        }

        [Test]
        public void SelectIndicesToDraw_SampleCountAtOrBelowMax_ReturnsEveryIndex()
        {
            IReadOnlyList<int> indices = BeanVisualizer.SelectIndicesToDraw(5, 10);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, indices);
        }

        [Test]
        public void SelectIndicesToDraw_SampleCountExceedsMax_CapsCountAndKeepsFirstAndLast()
        {
            IReadOnlyList<int> indices = BeanVisualizer.SelectIndicesToDraw(1000, 100);

            Assert.AreEqual(100, indices.Count);
            Assert.AreEqual(0, indices[0]);
            Assert.AreEqual(999, indices[indices.Count - 1]);
        }

        [Test]
        public void SelectIndicesToDraw_NoSamples_ReturnsEmpty()
        {
            IReadOnlyList<int> indices = BeanVisualizer.SelectIndicesToDraw(0, 100);

            Assert.AreEqual(0, indices.Count);
        }

        [Test]
        public void ResolveColor_ColorModeNone_ReturnsConfiguredPathColor()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.ColorMode = BeanColorMode.None;
            visualizer.PathColor = Color.magenta;

            var samples = new List<BeanSample>
            {
                new BeanSample(0, 0f, Vector3.zero, Quaternion.identity),
                new BeanSample(1, 1f, Vector3.one, Quaternion.identity)
            };

            Color color = visualizer.ResolveColor(1, samples);

            Object.DestroyImmediate(go);

            Assert.AreEqual(Color.magenta, color);
        }

        [Test]
        public void ResolveColor_ColorModeByTime_InterpolatesFromFirstToLastTimestamp()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.ColorMode = BeanColorMode.ByTime;

            var samples = new List<BeanSample>
            {
                new BeanSample(0, 0f, Vector3.zero, Quaternion.identity),
                new BeanSample(1, 5f, Vector3.zero, Quaternion.identity),
                new BeanSample(2, 10f, Vector3.zero, Quaternion.identity)
            };

            Color first = visualizer.ResolveColor(0, samples);
            Color last = visualizer.ResolveColor(2, samples);

            Object.DestroyImmediate(go);

            Assert.AreEqual(Color.blue, first);
            Assert.AreEqual(Color.red, last);
        }

        [Test]
        public void ResolveColor_ColorModeBySpeed_FasterSegmentSkewsRedderThanSlowerSegment()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.ColorMode = BeanColorMode.BySpeed;

            // Segment 0->1 covers 1 unit in 1s (slow); segment 1->2 covers 10 units in 1s (fast).
            var samples = new List<BeanSample>
            {
                new BeanSample(0, 0f, Vector3.zero, Quaternion.identity),
                new BeanSample(1, 1f, new Vector3(1f, 0f, 0f), Quaternion.identity),
                new BeanSample(2, 2f, new Vector3(11f, 0f, 0f), Quaternion.identity)
            };

            Color slow = visualizer.ResolveColor(1, samples);
            Color fast = visualizer.ResolveColor(2, samples);

            Object.DestroyImmediate(go);

            Assert.AreEqual(Color.blue, slow);
            Assert.AreEqual(Color.red, fast);
        }

        [Test]
        public void DrawPath_TwoSamples_DrawsSingleLineWithConfiguredColor()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.Tracker = tracker;
            visualizer.ColorMode = BeanColorMode.None;
            visualizer.PathColor = Color.cyan;

            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(1f, 2f, 3f);
            go.transform.position = from;
            tracker.SimulateFrame(0f);
            go.transform.position = to;
            tracker.SimulateFrame(0f);

            var drawer = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawer);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, drawer.Lines.Count);
            Assert.AreEqual(from, drawer.Lines[0].From);
            Assert.AreEqual(to, drawer.Lines[0].To);
            Assert.AreEqual(Color.cyan, drawer.Lines[0].Color);
            Assert.AreEqual(0, drawer.Spheres.Count);
        }

        [Test]
        public void DrawPath_FewerThanTwoSamples_DrawsNothing()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.Tracker = tracker;

            var drawerWithZeroSamples = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawerWithZeroSamples);

            tracker.SimulateFrame(0f); // exactly one sample now

            var drawerWithOneSample = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawerWithOneSample);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(0, drawerWithZeroSamples.Lines.Count);
            Assert.AreEqual(0, drawerWithOneSample.Lines.Count);
        }

        [Test]
        public void DrawPath_NoTrackerAvailable_DrawsNothing()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.Tracker = null; // and no BeanTracker component on the GameObject either

            var drawer = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawer);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(0, drawer.Lines.Count);
            Assert.AreEqual(0, drawer.Spheres.Count);
        }

        [Test]
        public void DrawPath_SampleCountExceedsMax_DrawsDecimatedSegmentsMatchingSelectIndicesToDraw()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.Tracker = tracker;
            visualizer.MaxPointsToDraw = 3;

            const int sampleCount = 10;
            for (int i = 0; i < sampleCount; i++)
            {
                go.transform.position = new Vector3(i, 0f, 0f);
                tracker.SimulateFrame(0f);
            }

            IReadOnlyList<int> expectedIndices = BeanVisualizer.SelectIndicesToDraw(sampleCount, 3);
            IReadOnlyList<BeanSample> samples = tracker.Samples;

            var drawer = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawer);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(expectedIndices.Count - 1, drawer.Lines.Count);
            for (int i = 1; i < expectedIndices.Count; i++)
            {
                RecordingGizmoDrawer.LineCall segment = drawer.Lines[i - 1];
                Assert.AreEqual(samples[expectedIndices[i - 1]].Position, segment.From);
                Assert.AreEqual(samples[expectedIndices[i]].Position, segment.To);
            }
        }

        [Test]
        public void DrawPath_DrawPointsEnabled_AlsoDrawsSphereAtEachSelectedIndex()
        {
            var go = new GameObject("BeanVisualizerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var visualizer = go.AddComponent<BeanVisualizer>();
            visualizer.Tracker = tracker;
            visualizer.DrawPoints = true;
            visualizer.MaxPointsToDraw = 3;

            const int sampleCount = 10;
            for (int i = 0; i < sampleCount; i++)
            {
                go.transform.position = new Vector3(i, 0f, 0f);
                tracker.SimulateFrame(0f);
            }

            IReadOnlyList<int> expectedIndices = BeanVisualizer.SelectIndicesToDraw(sampleCount, 3);
            IReadOnlyList<BeanSample> samples = tracker.Samples;

            var drawer = new RecordingGizmoDrawer();
            visualizer.DrawPath(drawer);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(expectedIndices.Count, drawer.Spheres.Count);
            for (int i = 0; i < expectedIndices.Count; i++)
            {
                Assert.AreEqual(samples[expectedIndices[i]].Position, drawer.Spheres[i].Center);
                Assert.AreEqual(0.05f, drawer.Spheres[i].Radius); // matches BeanVisualizer.GizmoPointRadius
            }
        }
    }
}
