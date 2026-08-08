using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanSnapshotExporterTests
    {
        [Test]
        public void BuildPathPositions_ReturnsPositionsInChronologicalOrder()
        {
            var samples = new List<BeanSample>
            {
                new BeanSample(0, 0f, new Vector3(1f, 0f, 0f), Quaternion.identity),
                new BeanSample(1, 1f, new Vector3(2f, 0f, 0f), Quaternion.identity),
                new BeanSample(2, 2f, new Vector3(3f, 0f, 0f), Quaternion.identity)
            };

            Vector3[] positions = BeanSnapshotExporter.BuildPathPositions(samples);

            CollectionAssert.AreEqual(
                new[] { new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f) },
                positions);
        }

        [Test]
        public void BuildPathPositions_NoSamples_ReturnsEmptyArray()
        {
            Vector3[] positions = BeanSnapshotExporter.BuildPathPositions(new List<BeanSample>());

            Assert.AreEqual(0, positions.Length);
        }

        [Test]
        public void IsBufferAtCapacity_SampleCountBelowMax_ReturnsFalse()
        {
            Assert.IsFalse(BeanSnapshotExporter.IsBufferAtCapacity(500, 1000));
        }

        [Test]
        public void IsBufferAtCapacity_SampleCountAtMax_ReturnsTrue()
        {
            // T28 (TESTS/TestTracker.md): a full ring buffer means the oldest samples have started
            // being overwritten - "at capacity" itself is the signal, not just "over" it.
            Assert.IsTrue(BeanSnapshotExporter.IsBufferAtCapacity(1000, 1000));
        }

        [Test]
        public void IsBufferAtCapacity_ZeroSamples_ReturnsFalseRegardlessOfMax()
        {
            // A zero/near-zero maxSamples configuration shouldn't spuriously warn on an empty buffer.
            Assert.IsFalse(BeanSnapshotExporter.IsBufferAtCapacity(0, 0));
        }

        [Test]
        public void ComputePathBounds_EncapsulatesEveryPosition()
        {
            var samples = new List<BeanSample>
            {
                new BeanSample(0, 0f, new Vector3(-5f, 0f, 2f), Quaternion.identity),
                new BeanSample(1, 1f, new Vector3(5f, 3f, -2f), Quaternion.identity)
            };

            Bounds bounds = BeanSnapshotExporter.ComputePathBounds(samples);

            Assert.AreEqual(new Vector3(0f, 1.5f, 0f), bounds.center);
            Assert.AreEqual(new Vector3(10f, 3f, 4f), bounds.size);
        }

        [Test]
        public void ComputePathBounds_NoSamples_ReturnsZeroSizedBounds()
        {
            Bounds bounds = BeanSnapshotExporter.ComputePathBounds(new List<BeanSample>());

            Assert.AreEqual(Vector3.zero, bounds.size);
        }

        [Test]
        public void IsFlatPath_NearZeroDepth_ReturnsTrue()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 0f));

            Assert.IsTrue(BeanSnapshotExporter.IsFlatPath(bounds));
        }

        [Test]
        public void IsFlatPath_RealDepth_ReturnsFalse()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));

            Assert.IsFalse(BeanSnapshotExporter.IsFlatPath(bounds));
        }

        [Test]
        public void ComputeFraming_FlatPath_ReturnsOrthographicFrontOnView()
        {
            var bounds = new Bounds(new Vector3(1f, 2f, 0f), new Vector3(10f, 4f, 0f));

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFraming(bounds, Vector3.right, true, 16f / 9f, 60f, 2f);

            Assert.IsTrue(framing.Orthographic);
            Assert.Greater(framing.OrthographicSize, 0f);
            Assert.AreEqual(bounds.center.x, framing.Position.x, 0.001f);
            Assert.AreEqual(bounds.center.y, framing.Position.y, 0.001f);
            Assert.Less(framing.Position.z, bounds.center.z); // sits behind the path, looking toward it
        }

        [Test]
        public void ComputeFraming_ThreeDimensionalPath_ReturnsPerspectiveViewLookingAtCenter()
        {
            var bounds = new Bounds(new Vector3(5f, 0f, 5f), new Vector3(20f, 10f, 20f));

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFraming(bounds, new Vector3(1f, 0f, 0f), false, 16f / 9f, 60f, 2f);

            Assert.IsFalse(framing.Orthographic);
            AreRoughlyEqual(bounds.center, framing.Position + framing.Rotation * Vector3.forward * Vector3.Distance(framing.Position, bounds.center));
        }

        [Test]
        public void ComputeFraming_LargerBounds_PlacesCameraFartherAway()
        {
            var small = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));
            var large = new Bounds(Vector3.zero, new Vector3(40f, 40f, 40f));

            SnapshotFraming smallFraming = BeanSnapshotExporter.ComputeFraming(small, Vector3.right, false, 16f / 9f, 60f, 2f);
            SnapshotFraming largeFraming = BeanSnapshotExporter.ComputeFraming(large, Vector3.right, false, 16f / 9f, 60f, 2f);

            Assert.Greater(largeFraming.Position.magnitude, smallFraming.Position.magnitude);
        }

        [Test]
        public void ComputeFraming_NearZeroBoundsWithLargerMinFramingRadius_FramesFartherBack()
        {
            // T23 (TESTS/TestTracker.md): a near-stationary path's tiny bounds produced a
            // useless close-up under the old fixed 2f literal. A larger configured
            // minFramingRadius should push the camera back farther for the exact same bounds.
            var nearZeroBounds = new Bounds(Vector3.zero, new Vector3(0.01f, 0.01f, 0.01f));

            SnapshotFraming small = BeanSnapshotExporter.ComputeFraming(nearZeroBounds, Vector3.right, false, 16f / 9f, 60f, 2f);
            SnapshotFraming large = BeanSnapshotExporter.ComputeFraming(nearZeroBounds, Vector3.right, false, 16f / 9f, 60f, 10f);

            Assert.Greater(Vector3.Distance(large.Position, nearZeroBounds.center), Vector3.Distance(small.Position, nearZeroBounds.center));
        }

        [Test]
        public void ComputeFraming_NearZeroFlatBoundsWithLargerMinFramingRadius_FramesWithLargerOrthoSize()
        {
            var nearZeroBounds = new Bounds(Vector3.zero, new Vector3(0.01f, 0.01f, 0f));

            SnapshotFraming small = BeanSnapshotExporter.ComputeFraming(nearZeroBounds, Vector3.right, true, 16f / 9f, 60f, 2f);
            SnapshotFraming large = BeanSnapshotExporter.ComputeFraming(nearZeroBounds, Vector3.right, true, 16f / 9f, 60f, 10f);

            Assert.Greater(large.OrthographicSize, small.OrthographicSize);
        }

        [Test]
        public void ComputeFraming_PathTravelsParallelToOldFixedOffsetDirection_StillFramesBroadside()
        {
            // (1,0,1) is exactly the horizontal direction the old hardcoded IsoDirection offset
            // used - found live in little wings (T17/T18) to foreshorten into a thick stripe
            // when a path travels roughly parallel to it. The fix derives the offset from the
            // path's own travel direction instead, so it should now always land perpendicular.
            var bounds = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
            var travelDirection = new Vector3(1f, 0f, 1f);

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFraming(bounds, travelDirection, false, 16f / 9f, 60f, 2f);

            Vector3 horizontalOffset = new Vector3(framing.Position.x - bounds.center.x, 0f, framing.Position.z - bounds.center.z);
            float alignment = Vector3.Dot(horizontalOffset.normalized, travelDirection.normalized);

            Assert.Less(Mathf.Abs(alignment), 0.1f, "camera offset should be roughly perpendicular to the path's travel direction");
        }

        [Test]
        public void ComputeFraming_NoHorizontalTravel_FallsBackToForwardBroadsideWithoutError()
        {
            // A purely vertical path - e.g. a platformer jump/climb with no horizontal movement -
            // has no travel direction to derive a broadside offset from.
            var bounds = new Bounds(new Vector3(0f, 5f, 0f), new Vector3(1f, 10f, 1f));
            var travelDirection = new Vector3(0f, 1f, 0f);

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFraming(bounds, travelDirection, false, 16f / 9f, 60f, 2f);

            Assert.IsFalse(framing.Orthographic);
            Assert.Greater(Vector3.Distance(framing.Position, bounds.center), 0f);
        }

        [Test]
        public void ResolveIsFlat_ModeAuto_UsesIsFlatPathHeuristic()
        {
            var flatBounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 0f));
            var deepBounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));

            Assert.IsTrue(BeanSnapshotExporter.ResolveIsFlat(BeanDimensionMode.Auto, flatBounds));
            Assert.IsFalse(BeanSnapshotExporter.ResolveIsFlat(BeanDimensionMode.Auto, deepBounds));
        }

        [Test]
        public void ResolveIsFlat_ModeForce2D_AlwaysTrueRegardlessOfBounds()
        {
            var deepBounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));

            Assert.IsTrue(BeanSnapshotExporter.ResolveIsFlat(BeanDimensionMode.Force2D, deepBounds));
        }

        [Test]
        public void ResolveIsFlat_ModeForce3D_AlwaysFalseRegardlessOfBounds()
        {
            var flatBounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 0f));

            Assert.IsFalse(BeanSnapshotExporter.ResolveIsFlat(BeanDimensionMode.Force3D, flatBounds));
        }

        [Test]
        public void ResolveSnapshotPath_ExplicitPathSet_ReturnsItUnchanged()
        {
            string path = BeanSnapshotExporter.ResolveSnapshotPath("C:/somewhere/custom.png", "AnyName", "abc12345", DateTime.UtcNow);

            Assert.AreEqual("C:/somewhere/custom.png", path);
        }

        [Test]
        public void ResolveSnapshotPath_NoExplicitPath_DefaultsUnderProjectRootSubfolderWithTimestampAndName()
        {
            var captureTime = new DateTime(2026, 8, 7, 13, 45, 30, 250, DateTimeKind.Utc);

            string path = BeanSnapshotExporter.ResolveSnapshotPath(null, "PlayerPlane", "abc12345", captureTime);

            StringAssert.StartsWith(BeanArtifactPaths.RootDirectory, path);
            StringAssert.DoesNotContain("AppData", path);
            StringAssert.Contains("BeanSnapshots", path);
            StringAssert.Contains("20260807_134530_250_PlayerPlane_abc12345_bean_snapshot.png", path);
        }

        [Test]
        public void ResolveSnapshotPath_TwoCapturesAtDifferentTimes_ProduceDifferentPaths()
        {
            var firstCapture = new DateTime(2026, 8, 7, 13, 45, 30, DateTimeKind.Utc);
            var secondCapture = firstCapture.AddSeconds(1);

            string firstPath = BeanSnapshotExporter.ResolveSnapshotPath(null, "PlayerPlane", "abc12345", firstCapture);
            string secondPath = BeanSnapshotExporter.ResolveSnapshotPath(null, "PlayerPlane", "abc12345", secondCapture);

            Assert.AreNotEqual(firstPath, secondPath);
        }

        [Test]
        public void ResolveSnapshotPath_TwoInstancesAtSameTimestamp_ProduceDifferentPaths()
        {
            var captureTime = new DateTime(2026, 8, 7, 13, 45, 30, DateTimeKind.Utc);

            string firstPath = BeanSnapshotExporter.ResolveSnapshotPath(null, "Bullet(Clone)", "token0001", captureTime);
            string secondPath = BeanSnapshotExporter.ResolveSnapshotPath(null, "Bullet(Clone)", "token0002", captureTime);

            Assert.AreNotEqual(firstPath, secondPath);
        }

        [Test]
        public void ApplyConfigDefaults_NullConfig_LeavesFieldsUnchanged()
        {
            var go = new GameObject("BeanSnapshotExporterTestObject");
            var exporter = go.AddComponent<BeanSnapshotExporter>();
            exporter.DimensionMode = BeanDimensionMode.Force3D;
            exporter.MinFramingRadius = 7.5f;

            exporter.ApplyConfigDefaults(null);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanDimensionMode.Force3D, exporter.DimensionMode);
            Assert.AreEqual(7.5f, exporter.MinFramingRadius);
        }

        [Test]
        public void ApplyConfigDefaults_WithConfig_AppliesDimensionModeAndMinFramingRadius()
        {
            var go = new GameObject("BeanSnapshotExporterTestObject");
            var exporter = go.AddComponent<BeanSnapshotExporter>();

            var config = new BeanConfig { DefaultDimensionMode = BeanDimensionMode.Force2D, DefaultMinFramingRadius = 8f };

            exporter.ApplyConfigDefaults(config);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanDimensionMode.Force2D, exporter.DimensionMode);
            Assert.AreEqual(8f, exporter.MinFramingRadius);
        }

        [Test]
        public void ComputeFramingForAngle_Auto_MatchesComputeFraming()
        {
            var bounds = new Bounds(new Vector3(5f, 0f, 5f), new Vector3(20f, 10f, 20f));
            var travelDirection = new Vector3(1f, 0f, 0.5f);

            SnapshotFraming viaFraming = BeanSnapshotExporter.ComputeFraming(bounds, travelDirection, false, 16f / 9f, 60f, 2f);
            SnapshotFraming viaAngle = BeanSnapshotExporter.ComputeFramingForAngle(bounds, travelDirection, false, 16f / 9f, 60f, 2f, BeanSnapshotAngle.Auto);

            AreRoughlyEqual(viaFraming.Position, viaAngle.Position);
            Assert.AreEqual(viaFraming.Orthographic, viaAngle.Orthographic);
        }

        [Test]
        public void ComputeFramingForAngle_Side_MatchesThreeDimensionalComputeFraming()
        {
            // Side is meant to be exactly the same broadside placement Auto already uses for a
            // real 3D path - ComputeFramingForAngle should produce an identical result regardless
            // of isFlat, since Side always forces the perspective/broadside branch.
            var bounds = new Bounds(new Vector3(5f, 0f, 5f), new Vector3(20f, 10f, 20f));
            var travelDirection = new Vector3(1f, 0f, 0.5f);

            SnapshotFraming viaFraming = BeanSnapshotExporter.ComputeFraming(bounds, travelDirection, false, 16f / 9f, 60f, 2f);
            SnapshotFraming viaAngle = BeanSnapshotExporter.ComputeFramingForAngle(bounds, travelDirection, true, 16f / 9f, 60f, 2f, BeanSnapshotAngle.Side);

            AreRoughlyEqual(viaFraming.Position, viaAngle.Position);
            Assert.IsFalse(viaAngle.Orthographic, "Side should always be a perspective shot, even for a flat path");
        }

        [Test]
        public void ComputeFramingForAngle_Above_LooksStraightDownFromDirectlyOverhead()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(10f, 2f, 10f));

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFramingForAngle(bounds, Vector3.right, false, 16f / 9f, 60f, 2f, BeanSnapshotAngle.Above);

            Assert.IsFalse(framing.Orthographic);
            Assert.AreEqual(bounds.center.x, framing.Position.x, 0.001f);
            Assert.AreEqual(bounds.center.z, framing.Position.z, 0.001f);
            Assert.Greater(framing.Position.y, bounds.center.y);
            AreRoughlyEqual(Vector3.down, framing.Rotation * Vector3.forward, 0.01f);
        }

        [Test]
        public void ComputeFramingForAngle_Behind_OpposesTravelDirection()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(10f, 2f, 10f));
            var travelDirection = new Vector3(1f, 0f, 0f);

            SnapshotFraming framing = BeanSnapshotExporter.ComputeFramingForAngle(bounds, travelDirection, false, 16f / 9f, 60f, 2f, BeanSnapshotAngle.Behind);

            Vector3 horizontalOffset = new Vector3(framing.Position.x - bounds.center.x, 0f, framing.Position.z - bounds.center.z);
            float alignment = Vector3.Dot(horizontalOffset.normalized, travelDirection.normalized);

            Assert.Less(alignment, -0.5f, "camera should sit on the opposite side of the path's travel direction");
        }

        [Test]
        public void ResolveMultiAngleSnapshotPath_IncludesGroupTimestampIndexAndAngleName()
        {
            var captureTime = new DateTime(2026, 8, 8, 10, 0, 0, 500, DateTimeKind.Utc);

            string path = BeanSnapshotExporter.ResolveMultiAngleSnapshotPath(null, "PlayerPlane", BeanSnapshotAngle.Above, 0, 3, "abc12345", captureTime);

            StringAssert.Contains("20260808_100000_500.1_PlayerPlane_Above_abc12345_bean_snapshot.png", path);
        }

        [Test]
        public void ResolveMultiAngleSnapshotPath_DifferentAnglesAtSameTimestamp_ProduceDifferentPaths()
        {
            var captureTime = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);

            string first = BeanSnapshotExporter.ResolveMultiAngleSnapshotPath(null, "PlayerPlane", BeanSnapshotAngle.Above, 0, 2, "abc12345", captureTime);
            string second = BeanSnapshotExporter.ResolveMultiAngleSnapshotPath(null, "PlayerPlane", BeanSnapshotAngle.Side, 1, 2, "abc12345", captureTime);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void ResolveMultiAngleSnapshotPath_IgnoresExplicitPathOverride()
        {
            // Unlike single-angle ResolveSnapshotPath, an explicit filePath can't safely back more
            // than one output file, so multi-angle capture always uses the default location.
            var captureTime = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);

            string path = BeanSnapshotExporter.ResolveMultiAngleSnapshotPath("C:/somewhere/custom.png", "PlayerPlane", BeanSnapshotAngle.Above, 0, 2, "abc12345", captureTime);

            StringAssert.StartsWith(BeanArtifactPaths.RootDirectory, path);
        }

        private static void AreRoughlyEqual(Vector3 expected, Vector3 actual, float tolerance = 0.01f)
        {
            Assert.Less(Vector3.Distance(expected, actual), tolerance);
        }
    }
}
