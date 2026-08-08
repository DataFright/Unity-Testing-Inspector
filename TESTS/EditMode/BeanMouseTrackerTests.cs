using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanMouseTrackerTests
    {
        [Test]
        public void ResolveScreenPosition_ReturnsScreenXYWithZeroZ()
        {
            Vector3 result = BeanMouseTracker.ResolveScreenPosition(new Vector2(640f, 360f));

            Assert.AreEqual(new Vector3(640f, 360f, 0f), result);
        }

        [Test]
        public void ResolveWorldPosition_NoCamera_ReturnsZero()
        {
            Vector3 result = BeanMouseTracker.ResolveWorldPosition(new Vector2(640f, 360f), null, 10f);

            Assert.AreEqual(Vector3.zero, result);
        }

        [Test]
        public void ResolveWorldPosition_WithCamera_MatchesScreenToWorldPoint()
        {
            var go = new GameObject("BeanMouseTrackerTestCamera");
            var camera = go.AddComponent<Camera>();

            var mouseScreenPosition = new Vector2(320f, 240f);
            Vector3 expected = camera.ScreenToWorldPoint(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 10f));
            Vector3 actual = BeanMouseTracker.ResolveWorldPosition(mouseScreenPosition, camera, 10f);

            Object.DestroyImmediate(go);

            Assert.AreEqual(expected, actual);
        }
    }
}
