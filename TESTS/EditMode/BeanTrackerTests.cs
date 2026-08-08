using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UTI.Tests
{
    public class BeanTrackerTests
    {
        [Test]
        public void SimulateFrame_EveryUpdate_CapturesPositionMatchingTransform()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryUpdate;
            tracker.ClearSamples();
            // startTrackingOnEnable only takes effect via OnEnable, which doesn't fire in Edit
            // Mode without [ExecuteAlways] (deliberately not used - see BeanTracker.cs). So
            // Edit Mode tests must arm tracking explicitly, same as any Edit Mode tooling would.
            tracker.StartTracking();

            var expectedPosition = new Vector3(1f, 2f, 3f);
            go.transform.position = expectedPosition;
            tracker.SimulateFrame(1f / 60f);

            int count = tracker.Samples.Count;
            Vector3 lastPosition = tracker.Samples[count - 1].Position;

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, count);
            Assert.AreEqual(expectedPosition, lastPosition);
        }

        [Test]
        public void SimulateFrame_EveryNSeconds_CapturesAtConfiguredInterval()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryNSeconds;
            tracker.CaptureInterval = 0.1f;
            tracker.ClearSamples();
            // startTrackingOnEnable only takes effect via OnEnable, which doesn't fire in Edit
            // Mode without [ExecuteAlways] (deliberately not used - see BeanTracker.cs). So
            // Edit Mode tests must arm tracking explicitly, same as any Edit Mode tooling would.
            tracker.StartTracking();

            // 11 frames of 0.05s = 0.55s elapsed -> floor(0.55 / 0.1) = 5 captures
            for (int i = 0; i < 11; i++)
                tracker.SimulateFrame(0.05f);

            int count = tracker.Samples.Count;
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(5, count);
        }

        [Test]
        public void SimulateFixedFrame_EveryFixedUpdate_CapturesOnceWhileTracking()
        {
            // T12 (TESTS/TestTracker.md): EveryFixedUpdate previously had no automated coverage -
            // FixedUpdate() bypassed the testable SimulateFrame() path entirely. Now it's driven
            // through SimulateFixedFrame() the same way EveryUpdate/EveryNSeconds go through
            // SimulateFrame(), so this is testable without a real physics tick.
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryFixedUpdate;
            tracker.ClearSamples();
            tracker.StartTracking();

            tracker.SimulateFixedFrame();
            tracker.SimulateFixedFrame();

            int count = tracker.Samples.Count;
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void SimulateFixedFrame_NotTracking_DoesNotCapture()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryFixedUpdate;
            tracker.ClearSamples();
            // Deliberately not calling StartTracking() - isTracking stays false.

            tracker.SimulateFixedFrame();

            int count = tracker.Samples.Count;
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void SimulateFixedFrame_WrongCaptureMode_DoesNotCapture()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryUpdate; // not EveryFixedUpdate
            tracker.ClearSamples();
            tracker.StartTracking();

            tracker.SimulateFixedFrame();

            int count = tracker.Samples.Count;
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(0, count, "EveryFixedUpdate-only capture should not fire under a different capture mode");
        }

        [Test]
        public void StopTracking_HaltsFurtherCaptures()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryUpdate;
            tracker.ClearSamples();
            // startTrackingOnEnable only takes effect via OnEnable, which doesn't fire in Edit
            // Mode without [ExecuteAlways] (deliberately not used - see BeanTracker.cs). So
            // Edit Mode tests must arm tracking explicitly, same as any Edit Mode tooling would.
            tracker.StartTracking();

            tracker.SimulateFrame(1f / 60f);
            tracker.StopTracking();
            int countAtStop = tracker.Samples.Count;

            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);
            int countAfterMoreFrames = tracker.Samples.Count;

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, countAtStop, "expected exactly one capture before StopTracking()");
            Assert.AreEqual(countAtStop, countAfterMoreFrames, "count should not change after StopTracking()");
        }

        [Test]
        public void OnSample_FiresOnceCaptureHappens()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryUpdate;
            tracker.ClearSamples();
            // startTrackingOnEnable only takes effect via OnEnable, which doesn't fire in Edit
            // Mode without [ExecuteAlways] (deliberately not used - see BeanTracker.cs). So
            // Edit Mode tests must arm tracking explicitly, same as any Edit Mode tooling would.
            tracker.StartTracking();

            int fireCount = 0;
            tracker.OnSample += _ => fireCount++;

            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(2, fireCount);
        }

        [Test]
        public void SimulateFrame_CustomCaptureAssigned_PopulatesExtrasOnTheSample()
        {
            // T11 (TESTS/TestTracker.md): previously only "Extras is null when unassigned" was
            // tested - this exercises the actual delegate -> Capture() -> sample pipeline.
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryUpdate;
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.CustomCapture = _ => new Dictionary<string, float> { { "health", 42f }, { "ammo", 7f } };

            tracker.SimulateFrame(1f / 60f);

            Dictionary<string, float> extras = tracker.Samples[0].Extras;

            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsNotNull(extras);
            Assert.AreEqual(42f, extras["health"]);
            Assert.AreEqual(7f, extras["ammo"]);
        }

        [Test]
        public void SimulateFrame_CustomCaptureThrows_StillCapturesSampleWithoutExtras()
        {
            // A throwing CustomCapture delegate is a real system-boundary failure (caller-supplied
            // code, not internal UTI logic) - tracking itself should survive it: the sample still
            // gets captured (tick advances, OnSample fires), just without extras for that tick.
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.CustomCapture = _ => throw new InvalidOperationException("boom");

            int fireCount = 0;
            tracker.OnSample += _ => fireCount++;

            var expectedWarning = new System.Text.RegularExpressions.Regex(@"^\[Bean\] CustomCapture threw.*");
            LogAssert.Expect(LogType.Warning, expectedWarning);
            tracker.SimulateFrame(1f / 60f);
            LogAssert.Expect(LogType.Warning, expectedWarning);
            tracker.SimulateFrame(1f / 60f);

            int count = tracker.Samples.Count;
            Dictionary<string, float> extras = tracker.Samples[0].Extras;

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(2, count, "capture should keep advancing even though the delegate throws every time");
            Assert.AreEqual(2, fireCount, "OnSample should still fire for every capture");
            Assert.IsNull(extras);
        }

        [Test]
        public void SimulateFrame_NoCustomCapture_ExtrasStaysNull()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            tracker.SimulateFrame(1f / 60f);

            Dictionary<string, float> extras = tracker.Samples[0].Extras;

            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsNull(extras);
        }

        [Test]
        public void OnStopTracking_FiresOnceOnActualTransition_NotOnRedundantCalls()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.StartTracking();

            int fireCount = 0;
            tracker.OnStopTracking += () => fireCount++;

            tracker.StopTracking();
            tracker.StopTracking(); // already stopped - should not fire again

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void MultipleTrackers_SimulatedIndependently_KeepFullyIndependentBuffers()
        {
            // T15 (TESTS/TestTracker.md), EditMode half: several simultaneously-tracked objects
            // should never share state - no shared statics anywhere in BeanTracker/BeanBuffer, so
            // each instance's Samples should reflect only its own object regardless of how many
            // other trackers are also active and being driven in the same test.
            var goA = new GameObject("BeanTrackerTestObjectA");
            var trackerA = goA.AddComponent<BeanTracker>();
            trackerA.ClearSamples();
            trackerA.StartTracking();

            var goB = new GameObject("BeanTrackerTestObjectB");
            var trackerB = goB.AddComponent<BeanTracker>();
            trackerB.ClearSamples();
            trackerB.StartTracking();

            var goC = new GameObject("BeanTrackerTestObjectC");
            var trackerC = goC.AddComponent<BeanTracker>();
            trackerC.ClearSamples();
            // trackerC deliberately not started - should stay empty throughout.

            goA.transform.position = new Vector3(1f, 0f, 0f);
            goB.transform.position = new Vector3(0f, 5f, 0f);

            trackerA.SimulateFrame(1f / 60f);
            goA.transform.position = new Vector3(2f, 0f, 0f);
            trackerA.SimulateFrame(1f / 60f);

            trackerB.SimulateFrame(1f / 60f);
            trackerC.SimulateFrame(1f / 60f);

            int countA = trackerA.Samples.Count;
            int countB = trackerB.Samples.Count;
            int countC = trackerC.Samples.Count;
            Vector3 lastPositionA = trackerA.Samples[countA - 1].Position;
            Vector3 lastPositionB = trackerB.Samples[countB - 1].Position;

            UnityEngine.Object.DestroyImmediate(goA);
            UnityEngine.Object.DestroyImmediate(goB);
            UnityEngine.Object.DestroyImmediate(goC);

            Assert.AreEqual(2, countA, "trackerA should have its own two captures, unaffected by B/C");
            Assert.AreEqual(1, countB, "trackerB should have exactly one capture of its own");
            Assert.AreEqual(0, countC, "trackerC never started tracking, so it should have captured nothing");
            Assert.AreEqual(new Vector3(2f, 0f, 0f), lastPositionA);
            Assert.AreEqual(new Vector3(0f, 5f, 0f), lastPositionB);
        }

        [Test]
        public void ApplyConfigDefaults_NullConfig_LeavesFieldsUnchanged()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.CaptureMode = BeanCaptureMode.EveryFixedUpdate;
            tracker.CaptureInterval = 1.5f;

            tracker.ApplyConfigDefaults(null);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanCaptureMode.EveryFixedUpdate, tracker.CaptureMode);
            Assert.AreEqual(1.5f, tracker.CaptureInterval);
        }

        [Test]
        public void ApplyConfigDefaults_WithConfig_AppliesCaptureModeAndInterval()
        {
            var go = new GameObject("BeanTrackerTestObject");
            var tracker = go.AddComponent<BeanTracker>();

            var config = new BeanConfig
            {
                DefaultCaptureMode = BeanCaptureMode.EveryNSeconds,
                DefaultCaptureInterval = 2.5f
            };

            tracker.ApplyConfigDefaults(config);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanCaptureMode.EveryNSeconds, tracker.CaptureMode);
            Assert.AreEqual(2.5f, tracker.CaptureInterval);
        }
    }
}
