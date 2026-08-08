using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UTI.Tests
{
    public class BeanLoggerTests
    {
        private class SpyBeanOutput : IBeanOutput
        {
            public int OpenCount;
            public int CloseCount;
            public readonly List<BeanSample> WrittenSamples = new List<BeanSample>();

            public void Open(BeanTracker tracker) => OpenCount++;
            public void Write(BeanSample sample) => WrittenSamples.Add(sample);
            public void Close() => CloseCount++;
        }

        private class ThrowingBeanOutput : IBeanOutput
        {
            public bool ThrowOnOpen;
            public bool ThrowOnWrite;
            public bool ThrowOnClose;
            public int WriteCount;
            public int CloseCount;

            public void Open(BeanTracker tracker)
            {
                if (ThrowOnOpen)
                    throw new InvalidOperationException("open boom");
            }

            public void Write(BeanSample sample)
            {
                WriteCount++;
                if (ThrowOnWrite)
                    throw new InvalidOperationException("write boom");
            }

            public void Close()
            {
                CloseCount++;
                if (ThrowOnClose)
                    throw new InvalidOperationException("close boom");
            }
        }

        [Test]
        public void Open_ForwardsCapturedSamplesToActiveOutputs()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.OpenCount);
            Assert.AreEqual(1, spy.WrittenSamples.Count);
        }

        [Test]
        public void Close_StopsForwardingAndClosesOutputs()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.CloseCount);
            Assert.AreEqual(1, spy.WrittenSamples.Count, "no samples should be forwarded after Close()");
        }

        [Test]
        public void ConsoleOutput_LogsFormattedSample()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Console;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Bean\].*tick=0.*"));
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CsvOutput_WritesHeaderAndOneRowPerSample()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual("tick,timestamp,x,y,z,qx,qy,qz,qw,extras", lines[0]);
            Assert.AreEqual(3, lines.Length, "expected a header row plus one row per sample");
            StringAssert.StartsWith("0,", lines[1]);
            StringAssert.StartsWith("1,", lines[2]);
        }

        [Test]
        public void CsvOutput_CustomCaptureExtras_WritesExtrasColumnWithRealData()
        {
            // T11 (TESTS/TestTracker.md): the delegate -> Capture() -> CsvBeanOutput pipeline,
            // not just "extras is empty when unassigned" (already covered by the header/row test
            // above via a tracker with no CustomCapture set).
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.CustomCapture = _ => new Dictionary<string, float> { { "health", 42f } };
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            StringAssert.Contains("health=42", lines[1]);
        }

        [Test]
        public void Open_OneOutputThrowsOnOpen_OtherOutputsStillOpenAndReceiveSamples()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnOpen = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to open.*"));
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.OpenCount, "the healthy output should still open despite the other one throwing");
            Assert.AreEqual(1, spy.WrittenSamples.Count, "the healthy output should still receive samples");
        }

        [Test]
        public void HandleSample_OneOutputThrowsOnWrite_OtherOutputStillReceivesSamplesAndBrokenOneIsDropped()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnWrite = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to write.*"));
            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, throwing.WriteCount, "the broken output should be dropped after its first failure, not retried every sample");
            Assert.AreEqual(2, spy.WrittenSamples.Count, "the healthy output should keep receiving every sample");
        }

        [Test]
        public void Close_OneOutputThrowsOnClose_OtherOutputStillCloses()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnClose = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);
            logger.Open();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to close.*"));
            logger.Close();

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, throwing.CloseCount);
            Assert.AreEqual(1, spy.CloseCount, "the healthy output should still close despite the other one throwing");
        }

        [Test]
        public void CsvOutput_DefaultAppendFalse_TruncatesOnReopen()
        {
            // T14 (TESTS/TestTracker.md): the original, still-default behavior - re-opening the
            // same path without append starts over, matching a pooled object getting a fresh log
            // per reuse unless it explicitly opts in to appending.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new CsvBeanOutput(path);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, lines.Length, "reopening without append should truncate back to just the header");
        }

        [Test]
        public void CsvOutput_AppendTrue_PreservesPriorRowsAcrossReopen()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new CsvBeanOutput(path, append: true);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Write(new BeanSample(1, 1f, Vector3.zero, Quaternion.identity));
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(3, lines.Length, "expected one header row plus both rows preserved across the reopen");
            Assert.AreEqual("tick,timestamp,x,y,z,qx,qy,qz,qw,extras", lines[0]);
            StringAssert.StartsWith("0,", lines[1]);
            StringAssert.StartsWith("1,", lines[2]);
        }

        [Test]
        public void BeanLogger_AppendAcrossReuseFalse_TruncatesAcrossOpenCloseCycles()
        {
            // Confirms the default (off) BeanLogger.AppendAcrossReuse threads append:false into
            // CsvBeanOutput exactly as before - a fixed shared path re-opened twice still starts
            // over, not accumulates.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            Assert.IsFalse(logger.AppendAcrossReuse, "should default to false");

            logger.Open();
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            logger.Open(); // simulates SetActive(false) -> SetActive(true) reuse
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, lines.Length, "expected just the header - reuse without opting in should not preserve prior rows");
        }

        [Test]
        public void BeanLogger_AppendAcrossReuseTrue_PreservesRowsAcrossOpenCloseCycles()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.AppendAcrossReuse = true;

            logger.Open();
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            logger.Open(); // simulates SetActive(false) -> SetActive(true) reuse
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(3, lines.Length, "expected the header plus one row from each reuse cycle, preserved across reopen");
        }

        [Test]
        public void ResolveFilePath_ExplicitPathSet_ReturnsItUnchanged()
        {
            string path = BeanLogger.ResolveFilePath("C:/somewhere/custom.csv", "AnyName", "abc12345", System.DateTime.UtcNow);

            Assert.AreEqual("C:/somewhere/custom.csv", path);
        }

        [Test]
        public void ResolveFilePath_NoExplicitPath_DefaultsUnderProjectRootSubfolderWithTimestampAndToken()
        {
            var captureTime = new System.DateTime(2026, 8, 7, 13, 45, 30, 250, System.DateTimeKind.Utc);

            string path = BeanLogger.ResolveFilePath(null, "PlayerPlane", "abc12345", captureTime);

            StringAssert.StartsWith(BeanArtifactPaths.RootDirectory, path);
            StringAssert.DoesNotContain("AppData", path);
            StringAssert.Contains("BeanLogs", path);
            StringAssert.Contains("20260807_134530_250_PlayerPlane_abc12345_bean.csv", path);
        }

        [Test]
        public void ResolveFilePath_TwoInstancesAtSameTimestamp_ProduceDifferentPaths()
        {
            var captureTime = new System.DateTime(2026, 8, 7, 13, 45, 30, System.DateTimeKind.Utc);

            string firstPath = BeanLogger.ResolveFilePath(null, "Bullet(Clone)", "token0001", captureTime);
            string secondPath = BeanLogger.ResolveFilePath(null, "Bullet(Clone)", "token0002", captureTime);

            Assert.AreNotEqual(firstPath, secondPath);
        }
    }
}
