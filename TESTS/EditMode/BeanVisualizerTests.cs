using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanVisualizerTests
    {
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
    }
}
