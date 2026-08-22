using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UTI.Tests
{
    // Covers BUG-05's Update() guard live, across real frames - the pure ResolveScreenPosition/
    // ResolveWorldPosition math already has EditMode coverage in BeanMouseTrackerTests. Which of
    // the two guarded tests below actually compiles depends on this project's own Active Input
    // Handling setting (Project Settings > Player), same symbol BeanMouseTracker.Update() itself
    // branches on - so a single project's test run only ever exercises the branch it's actually
    // configured for.
    public class BeanMouseTrackerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Update_NeverThrows_RegardlessOfInputHandlingMode()
        {
            var go = new GameObject("BeanMouseTrackerPlayModeTestObject");
            go.AddComponent<BeanMouseTracker>();

            yield return null;
            yield return null;
            yield return null;

            Object.Destroy(go);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        [UnityTest]
        public IEnumerator Update_WithLegacyInputAvailable_MatchesResolveScreenPositionOfCurrentMouse()
        {
            var go = new GameObject("BeanMouseTrackerPlayModeTestObject");
            var tracker = go.AddComponent<BeanMouseTracker>();
            tracker.TrackingSpace = BeanMouseTrackingSpace.Screen;

            yield return null;

            Vector3 expected = BeanMouseTracker.ResolveScreenPosition(Input.mousePosition);
            Assert.AreEqual(expected, go.transform.position);

            Object.Destroy(go);
        }
#else
        [UnityTest]
        public IEnumerator Update_WithoutLegacyInputAvailable_WarnsExactlyOnceAndHoldsScreenOrigin()
        {
            int warningCount = 0;
            void CountWarnings(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.Contains("BeanMouseTracker needs the legacy Input Manager"))
                    warningCount++;
            }

            Application.logMessageReceived += CountWarnings;

            var go = new GameObject("BeanMouseTrackerPlayModeTestObject");
            go.AddComponent<BeanMouseTracker>();

            yield return null;
            yield return null;
            yield return null;

            Application.logMessageReceived -= CountWarnings;

            Assert.AreEqual(
                1,
                warningCount,
                "BeanMouseTracker should warn exactly once about missing legacy input, not every frame.");
            Assert.AreEqual(Vector3.zero, go.transform.position);

            Object.Destroy(go);
        }
#endif
    }
}
